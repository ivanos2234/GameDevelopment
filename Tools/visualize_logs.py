#!/usr/bin/env python3
"""
Ninja Spinner - gameplay log visualizer.

Reads the session_*.log files written by GameLogger and builds one HTML report (charts + tables):
  * per level attempt: movement map, saw handling over time, enemy distance / risk over time
  * across all logs: learning curves, level results (max level), session time

Usage (Windows):
    pip install matplotlib numpy
    python visualize_logs.py                      # reads the game's default log folder
    python visualize_logs.py path\\to\\logs        # or any folder / list of .log files
    python visualize_logs.py logs -o my_report     # choose the output folder
Options: --no-open (don't open the report in the browser), --last N (only the N newest log files).
"""
import argparse
import base64
import glob
import html
import io
import os
import sys
import webbrowser
from collections import defaultdict

import numpy as np
import matplotlib

matplotlib.use("Agg")
import matplotlib.pyplot as plt  # noqa: E402
from matplotlib.collections import LineCollection  # noqa: E402
from matplotlib.colors import LinearSegmentedColormap  # noqa: E402

# ---------------------------------------------------------------- look

SURFACE = "#fcfcfb"
TEXT = "#0b0b0b"
TEXT2 = "#52514e"
MUTED = "#8a897f"
GRID = "#e6e5e0"
ZONE = "#f0efec"
BLUE = "#2a78d6"          # series 1
BLUE_LIGHT = "#86b6ef"
ORANGE = "#eb6834"        # series 2
AQUA = "#1baf7a"          # series 3
GOOD = "#0ca30c"          # status: won
CRITICAL = "#d03b3b"      # status: hit / lost
NEUTRAL = "#b5b4ac"       # other results
SEQ = LinearSegmentedColormap.from_list(
    "seq_blue", ["#86b6ef", "#5598e7", "#2a78d6", "#1c5cab", "#104281", "#0d366b"])

plt.rcParams.update({
    "figure.facecolor": SURFACE, "axes.facecolor": SURFACE, "savefig.facecolor": SURFACE,
    "axes.edgecolor": MUTED, "axes.labelcolor": TEXT2, "axes.titlecolor": TEXT,
    "axes.titlesize": 11, "axes.titleweight": "bold", "axes.titlelocation": "left",
    "axes.spines.top": False, "axes.spines.right": False,
    "axes.grid": True, "grid.color": GRID, "grid.linewidth": 0.8,
    "xtick.color": TEXT2, "ytick.color": TEXT2, "font.size": 9.5,
    "legend.frameon": False, "legend.fontsize": 8.5,
})

# Used when a file has no '#' header (the header is preferred: it always matches the file).
DEFAULT_SCHEMA = {
    "session_start": "playerId;gameVersion;platform;resolution;difficulty;tutorialDone;levelsCleared;totalPlayMin",
    "level_start": "scene;index;attempt;difficulty;timeLimit;enemies;killInOrder;hearts",
    "enemy": "enemyId;kind;x;y;killOrder",
    "move": "x;y;speed;heading;nearEnemy;nearShot;danger;hearts",
    "saw": "avgSpin;maxSpin;poweredPct;cappedPct;orbitDeg;reversals;extension",
    "saw_pass": "enemyId;kind;ticks;damage;avgSpin;impactSpeed;killed",
    "spin_up": "seconds",
    "danger": "phase;enemyId;kind;minDist;seconds",
    "close_call": "shooterKind;minGap",
    "hit": "source;kind;enemyId;hearts;x;y",
    "kill": "enemyId;kind;timeToKill;passes;dist;orderOk",
    "loss": "x;y;reason;lastHit;nearby;risk",
    "level_end": "scene;attempt;result;reason;elapsed;stars;hearts;enemiesLeft;newBest",
    "level_summary": "scene;attempt;distance;cells;stillPct;avgSpin;poweredPct;reversalsPerMin;spinUp;"
                     "passes;dmgPerPass;kills;risk;dangerPct;closeCalls;hits;lowHpDangerSec",
    "session_end": "duration;playTime;pausedTime;otherTime;attempts;maxReached;maxCleared;reason",
}
POWERED_SPIN = 650.0
CAP_SPIN = 1180.0
DANGER_RADIUS = 2.8

# ---------------------------------------------------------------- parsing


def value(text):
    if text == "-" or text == "":
        return None
    try:
        return float(text)
    except ValueError:
        return text


class Session:
    def __init__(self, path):
        self.path = path
        self.name = os.path.basename(path)
        self.start = None
        self.end = None
        self.attempts = []
        self.last_t = 0.0


class Attempt:
    def __init__(self, session, start):
        self.session = session
        self.start = start
        self.scene = start.get("scene")
        self.index = int(start.get("index") or -1)
        self.number = int(start.get("attempt") or 0)
        self.events = []
        self.enemies = {}
        self.end = None
        self.summary = None

    def of(self, name):
        return [e for e in self.events if e["ev"] == name]

    @property
    def label(self):
        return f"{self.scene} #{self.number}"

    @property
    def real_clock(self):
        """True when the level clock did not run for most of the attempt (the Tutorial holds it)."""
        moves = self.of("move")
        if len(moves) < 3:
            return False
        lt_span = (moves[-1].get("lt") or 0) - (moves[0].get("lt") or 0)
        t_span = moves[-1]["t"] - moves[0]["t"]
        return t_span > 0 and lt_span < 0.5 * t_span

    def clock(self, events):
        """x values for these events: level time, or seconds since the attempt started (real_clock)."""
        if self.real_clock:
            return np.array([e["t"] - self.start["t"] for e in events], dtype=float)
        return col(events, "lt")

    def at(self, event, key="lt"):
        return (event["t"] - self.start["t"]) if self.real_clock else event.get(key)

    @property
    def clock_label(self):
        return "seconds since level start" if self.real_clock else "level time (s)"

    @property
    def result(self):
        return self.end.get("result") if self.end else "unfinished"


def parse_header(line, schema):
    # "# move;x;y;speed;...  (every 0.5 s)"
    body = line[1:].strip().split("  ")[0].strip()
    parts = body.split(";")
    if len(parts) >= 2 and parts[0].replace("_", "").isalpha() and " " not in parts[0]:
        if parts[0] not in ("t",):  # skip the "t;lt;event;fields" explanation line
            schema[parts[0]] = parts[1:]


def read_session(path):
    schema = {k: v.split(";") for k, v in DEFAULT_SCHEMA.items()}
    session = Session(path)
    current = None
    with open(path, encoding="utf-8", errors="replace") as f:
        for raw in f:
            line = raw.strip()
            if not line:
                continue
            if line.startswith("#"):
                parse_header(line, schema)
                continue
            parts = line.split(";")
            if len(parts) < 3:
                continue
            ev = parts[2]
            e = {"t": value(parts[0]), "lt": value(parts[1]), "ev": ev}
            for name, text in zip(schema.get(ev, []), parts[3:]):
                e[name] = value(text)
            if isinstance(e["t"], float):
                session.last_t = max(session.last_t, e["t"])

            if ev == "session_start":
                session.start = e
            elif ev == "session_end":
                session.end = e
            elif ev == "level_start":
                current = Attempt(session, e)
                session.attempts.append(current)
            elif current is not None:
                current.events.append(e)
                if ev == "enemy":
                    current.enemies[e["enemyId"]] = e
                elif ev == "level_end":
                    current.end = e
                elif ev == "level_summary":
                    current.summary = e
                    current = None
    return session


def find_logs(paths, last):
    files = []
    for p in paths:
        if os.path.isdir(p):
            files += glob.glob(os.path.join(p, "session_*.log"))
        elif os.path.isfile(p):
            files.append(p)
    files = sorted(set(files))  # names start with date_time, so this is chronological
    return files[-last:] if last else files


def default_log_folder():
    base = os.environ.get("USERPROFILE") or os.path.expanduser("~")
    return os.path.join(base, "AppData", "LocalLow", "DefaultCompany", "GameDevelopment", "logs")

# ---------------------------------------------------------------- helpers


def col(events, name):
    return np.array([np.nan if e.get(name) is None else e.get(name) for e in events], dtype=float)


def fig_to_data_uri(fig, path):
    fig.savefig(path, dpi=110, bbox_inches="tight")
    buffer = io.BytesIO()
    fig.savefig(buffer, format="png", dpi=110, bbox_inches="tight")
    plt.close(fig)
    return "data:image/png;base64," + base64.b64encode(buffer.getvalue()).decode("ascii")


def legend_below(ax, ncol=4):
    ax.legend(loc="upper left", bbox_to_anchor=(0, -0.13), ncol=ncol, handlelength=1.6)


def fmt(v, digits=0, suffix=""):
    if v is None or (isinstance(v, float) and np.isnan(v)):
        return "–"
    if isinstance(v, str):
        return html.escape(v)
    return f"{v:,.{digits}f}{suffix}"


def fmt_time(seconds):
    if seconds is None:
        return "–"
    seconds = int(round(seconds))
    return f"{seconds // 60}:{seconds % 60:02d}"

# ---------------------------------------------------------------- per-attempt charts


def plot_map(att, level_moves):
    moves = att.of("move")
    fig, (a1, a2) = plt.subplots(1, 2, figsize=(12.5, 5.2))

    # Left: this attempt's path, coloured by level time.
    a1.set_title(f"Path - {att.label}")
    if len(moves) >= 2:
        xs, ys, lts = col(moves, "x"), col(moves, "y"), att.clock(moves)
        pts = np.column_stack([xs, ys])
        segments = np.stack([pts[:-1], pts[1:]], axis=1)
        lc = LineCollection(segments, cmap=SEQ, linewidths=2, capstyle="round")
        lc.set_array(lts[:-1])
        a1.add_collection(lc)
        bar = fig.colorbar(lc, ax=a1, fraction=0.04, pad=0.02)
        bar.set_label(att.clock_label)
        bar.outline.set_visible(False)
        a1.scatter([xs[0]], [ys[0]], s=60, color=SURFACE, edgecolors=TEXT, linewidths=1.5, zorder=4,
                   label="start")

        def player_at(lt):
            return np.interp(lt, lts, xs), np.interp(lt, lts, ys)

        kills = att.of("kill")
        if kills:
            kx, ky = zip(*(player_at(att.at(k)) for k in kills))
            a1.scatter(kx, ky, marker="*", s=170, color=ORANGE, edgecolors=SURFACE, linewidths=1,
                       zorder=6, label="kill (player position)")
    if att.enemies:
        ex = [e["x"] for e in att.enemies.values()]
        ey = [e["y"] for e in att.enemies.values()]
        a1.scatter(ex, ey, s=110, facecolors="none", edgecolors=TEXT2, linewidths=1.5, zorder=3,
                   label="enemy start")
        for e in att.enemies.values():
            a1.annotate(e["enemyId"], (e["x"], e["y"]), xytext=(0, 9), textcoords="offset points",
                        ha="center", fontsize=7.5, color=TEXT2)
    hits = att.of("hit")
    if hits:
        a1.scatter(col(hits, "x"), col(hits, "y"), marker="v", s=80, color=CRITICAL, edgecolors=SURFACE,
                   linewidths=1.5, zorder=7, label="hit taken")
    for loss in att.of("loss"):
        a1.scatter([loss["x"]], [loss["y"]], marker="X", s=140, color=TEXT, zorder=8, label="loss")
    a1.set_aspect("equal", adjustable="datalim")
    a1.autoscale_view()
    a1.set_xlabel("x (units)")
    a1.set_ylabel("y (units)")
    legend_below(a1)

    # Right: where time was spent, all attempts of this level.
    attempts_n = len(level_moves)
    xs_all = np.concatenate([col(m, "x") for m in level_moves]) if level_moves else np.array([])
    ys_all = np.concatenate([col(m, "y") for m in level_moves]) if level_moves else np.array([])
    a2.set_title(f"Time spent per 1x1 cell - all {attempts_n} attempt(s) of {att.scene}")
    if len(xs_all) > 1:
        step = 0.5
        ts = col(moves, "t")  # real time between samples (the level clock may be held)
        if len(ts) > 2:
            step = float(np.nanmedian(np.diff(ts)))
        x0, x1 = np.floor(np.nanmin(xs_all)) - 1, np.ceil(np.nanmax(xs_all)) + 1
        y0, y1 = np.floor(np.nanmin(ys_all)) - 1, np.ceil(np.nanmax(ys_all)) + 1
        h = a2.hist2d(xs_all, ys_all, bins=[np.arange(x0, x1 + 1), np.arange(y0, y1 + 1)],
                      weights=np.full(len(xs_all), step), cmap=SEQ, cmin=1e-6)
        bar = fig.colorbar(h[3], ax=a2, fraction=0.04, pad=0.02)
        bar.set_label("seconds")
        bar.outline.set_visible(False)
        if att.enemies:
            a2.scatter(ex, ey, s=110, facecolors="none", edgecolors=TEXT2, linewidths=1.5, zorder=3)
        a2.set_aspect("equal", adjustable="box")
        a2.grid(False)
    a2.set_xlabel("x (units)")
    a2.set_ylabel("y (units)")
    fig.tight_layout()
    return fig


def plot_saw(att):
    saws = att.of("saw")
    passes = att.of("saw_pass")
    fig, (a1, a2) = plt.subplots(2, 1, figsize=(12.5, 5.6), sharex=True,
                                 gridspec_kw={"height_ratios": [2.2, 1]})
    a1.set_title(f"Saw spin - {att.label}")
    lt = att.clock(saws)
    a1.plot(lt, col(saws, "maxSpin"), color=BLUE_LIGHT, lw=1.2, label="max spin in the second")
    a1.plot(lt, col(saws, "avgSpin"), color=BLUE, lw=2, label="average spin")
    for y, name in ((POWERED_SPIN, "powered 650°/s"), (CAP_SPIN, "damage cap 1180°/s")):
        a1.axhline(y, color=TEXT2, lw=1, ls=(0, (4, 3)))
        a1.annotate(name, (1, y), xycoords=("axes fraction", "data"), xytext=(4, 0),
                    textcoords="offset points", ha="left", va="center", fontsize=8, color=TEXT2)
    if passes:
        damage = col(passes, "damage")
        killed = col(passes, "killed") == 1
        sizes = 25 + np.nan_to_num(damage) * 7
        pt = att.clock(passes)
        a1.scatter(pt[~killed], col(passes, "avgSpin")[~killed], s=sizes[~killed],
                   color=ORANGE, edgecolors=SURFACE, linewidths=1.5, zorder=4,
                   label="saw pass through an enemy (size = damage)")
        if killed.any():
            a1.scatter(pt[killed], col(passes, "avgSpin")[killed], s=sizes[killed],
                       color=ORANGE, edgecolors=TEXT, linewidths=1.8, zorder=5, label="pass that killed")
    for s in att.of("spin_up"):
        a1.axvline(s["seconds"], color=AQUA, lw=1.5)
        a1.annotate(f"spin-up {s['seconds']:.1f}s", (s["seconds"], 1), xycoords=("data", "axes fraction"),
                    xytext=(4, -12), textcoords="offset points", fontsize=8, color=TEXT2)
    a1.set_ylabel("spin (°/s)")
    a1.set_ylim(bottom=0)
    a1.legend(loc="upper left", ncol=4, bbox_to_anchor=(0, -0.04))

    width = float(np.nanmedian(np.diff(lt))) * 0.8 if len(lt) > 2 else 0.8
    a2.bar(lt, col(saws, "poweredPct"), width=width, color=BLUE, label="% of the second at powered spin")
    rev = col(saws, "reversals")
    flips = np.nan_to_num(rev) > 0
    if flips.any():
        a2.scatter(lt[flips], np.full(flips.sum(), 104), marker="v", s=40, color=CRITICAL, zorder=5,
                   label="saw changed direction")
    a2.set_ylim(0, 112)
    a2.set_yticks([0, 50, 100])
    a2.set_ylabel("powered %")
    a2.set_xlabel(att.clock_label)
    a2.legend(loc="upper left", bbox_to_anchor=(0, -0.32), ncol=2)
    fig.tight_layout()
    return fig


def plot_risk(att):
    moves = att.of("move")
    fig, (a1, a2) = plt.subplots(2, 1, figsize=(12.5, 5.2), sharex=True,
                                 gridspec_kw={"height_ratios": [2.4, 1]})
    a1.set_title(f"Distance to enemies - {att.label}")
    lt = att.clock(moves)
    near = col(moves, "nearEnemy")
    shot = col(moves, "nearShot")
    top = max(6.0, float(np.nanmax(near)) if np.isfinite(near).any() else 6.0)
    a1.axhspan(0, DANGER_RADIUS, color=ZONE, zorder=0, label=f"danger zone (< {DANGER_RADIUS} u)")
    a1.plot(lt, near, color=BLUE, lw=2, label="nearest enemy")
    if np.isfinite(shot).any():
        a1.plot(lt, shot, color=AQUA, lw=1.5, label="nearest enemy shot")
    previous, drop_last = None, 10
    for i, k in enumerate(att.of("kill")):
        x = att.at(k)
        a1.axvline(x, color=MUTED, lw=1, ls=":")
        # Stagger labels of kills that come close together so they don't overlap.
        drop = 10 if previous is None or x - previous > 1.5 or drop_last == 22 else 22
        a1.annotate("kill", (x, top), xytext=(2, -drop), textcoords="offset points", fontsize=7.5, color=TEXT2)
        previous, drop_last = x, drop
    for h in att.of("hit"):
        x = att.at(h)
        a1.axvline(x, color=CRITICAL, lw=1.5)
        a1.annotate(f"hit ({h.get('enemyId') or h.get('source')})", (x, top), xytext=(3, -34),
                    textcoords="offset points", fontsize=7.5, color=CRITICAL)
    for c in att.of("close_call"):
        a1.scatter([att.at(c)], [0.15], marker="^", s=40, color=AQUA, zorder=5)
    a1.set_ylim(0, top * 1.05)
    a1.set_ylabel("distance (units)")
    a1.legend(loc="upper left", bbox_to_anchor=(0, -0.04), ncol=4)

    a2.step(lt, col(moves, "hearts"), where="post", color=BLUE, lw=2)
    hearts_max = att.start.get("hearts") or np.nanmax(col(moves, "hearts"))
    a2.set_ylim(0, (hearts_max or 5) + 0.5)
    a2.set_yticks(range(0, int(hearts_max or 5) + 1))
    a2.set_ylabel("hearts")
    a2.set_xlabel(att.clock_label)
    fig.tight_layout()
    return fig

# ---------------------------------------------------------------- overview charts


LEARNING = [
    ("poweredPct", "Powered saw time (%)", "higher = better"),
    ("reversalsPerMin", "Saw direction flips / min", "lower = smoother"),
    ("spinUp", "Spin-up time (s)", "lower = better"),
    ("dmgPerPass", "Damage per saw pass", "higher = better"),
    ("risk", "Risk score (0-100)", "how close to enemies"),
    ("hits", "Hits taken", "lower = better"),
]


def plot_learning(attempts):
    done = [a for a in attempts if a.summary and a.result in ("won", "lost")]
    fig, axes = plt.subplots(2, 3, figsize=(12.5, 6))
    labels = [f"{a.scene.replace('Level_', 'L')}·{a.number}" for a in done]
    x = np.arange(len(done))
    for ax, (key, title, hint) in zip(axes.flat, LEARNING):
        y = np.array([np.nan if a.summary.get(key) is None else a.summary[key] for a in done], dtype=float)
        ax.set_title(title)
        ax.set_title(hint, loc="right", fontsize=8, fontweight="normal", color=TEXT2)
        ax.plot(x, y, color=BLUE, lw=2, marker="o", ms=6, mfc=BLUE, mec=SURFACE, mew=1.5)
        if np.isfinite(y).sum() >= 3:  # trend line only when there is something to trend
            ok = np.isfinite(y)
            slope, icpt = np.polyfit(x[ok], y[ok], 1)
            ax.plot(x, slope * x + icpt, color=TEXT2, lw=1, ls=(0, (4, 3)))
        ax.set_xticks(x)
        ax.set_xticklabels(labels, rotation=45 if len(x) > 6 else 0, ha="right" if len(x) > 6 else "center",
                           fontsize=8)
        ax.set_xlim(-0.5, max(len(x), 3) - 0.5)
        ax.set_ylim(bottom=0)
    fig.tight_layout()
    return fig


RESULT_COLORS = [("won", GOOD), ("lost", CRITICAL), ("restart", NEUTRAL), ("menu", NEUTRAL),
                 ("quit", NEUTRAL), ("exit", NEUTRAL), ("unfinished", NEUTRAL)]


def plot_results(attempts):
    levels = sorted({a.index for a in attempts})
    names = {a.index: a.scene for a in attempts}
    fig, ax = plt.subplots(figsize=(12.5, 3.2))
    ax.set_title("Attempts per level by result")
    counts = defaultdict(lambda: defaultdict(int))
    for a in attempts:
        r = a.result if a.result in ("won", "lost") else "other"
        counts[a.index][r] += 1
    x = np.arange(len(levels))
    bottom = np.zeros(len(levels))
    for key, color, label in (("won", GOOD, "won"), ("lost", CRITICAL, "lost"),
                              ("other", NEUTRAL, "restarted / left / quit")):
        h = np.array([counts[l][key] for l in levels], dtype=float)
        if h.sum() == 0:
            continue
        ax.bar(x, h, bottom=bottom, width=0.6, color=color, edgecolor=SURFACE, linewidth=2, label=label)
        for xi, b, hi in zip(x, bottom, h):
            if hi:
                ax.text(xi, b + hi / 2, f"{int(hi)}", ha="center", va="center", fontsize=8.5,
                        color=SURFACE if key != "other" else TEXT)
        bottom += h
    ax.set_xticks(x)
    ax.set_xticklabels([names[l] for l in levels])
    ax.set_xlim(-0.5, max(len(levels), 8) - 0.5)
    ax.set_ylabel("attempts")
    ax.yaxis.get_major_locator().set_params(integer=True)
    ax.legend(loc="lower right", bbox_to_anchor=(1, 1.0), ncol=3)
    ax.grid(axis="x", visible=False)
    fig.tight_layout()
    return fig


def session_times(s):
    if s.end and s.end.get("duration") is not None:
        return (s.end.get("playTime") or 0, s.end.get("pausedTime") or 0, s.end.get("otherTime") or 0,
                s.end.get("reason") or "?")
    # No session_end yet (crash / still running): estimate from the attempts.
    play = sum((a.end.get("elapsed") or 0) for a in s.attempts if a.end)
    return play, 0.0, max(0.0, s.last_t - play), "no end line"


def plot_sessions(sessions):
    fig, ax = plt.subplots(figsize=(12.5, 1.4 + 0.45 * len(sessions)))
    ax.set_title("Session time")
    y = np.arange(len(sessions))[::-1]
    left = np.zeros(len(sessions))
    parts = [session_times(s) for s in sessions]
    for i, (name, color) in enumerate((("playing", BLUE), ("paused", ORANGE), ("menus / results / loading",
                                                                                NEUTRAL))):
        w = np.array([p[i] for p in parts], dtype=float) / 60
        ax.barh(y, w, left=left, height=0.55, color=color, edgecolor=SURFACE, linewidth=2, label=name)
        left += w
    for yi, total, p in zip(y, left, parts):
        ax.text(total, yi, f"  {total:.1f} min" + ("" if p[3] in ("quit",) else f"  ({p[3]})"),
                va="center", fontsize=8.5, color=TEXT2)
    ax.set_yticks(y)
    ax.set_yticklabels([s.name.replace("session_", "").replace(".log", "") for s in sessions], fontsize=8)
    ax.set_xlabel("minutes")
    ax.set_xlim(0, max(left.max() * 1.25, 1))
    ax.grid(axis="y", visible=False)
    ax.legend(loc="lower right", bbox_to_anchor=(1, 1.0), ncol=3)
    fig.tight_layout()
    return fig

# ---------------------------------------------------------------- report


CSS = """
body{font-family:Segoe UI,system-ui,sans-serif;margin:0;background:#fcfcfb;color:#0b0b0b}
main{max-width:1180px;margin:0 auto;padding:28px 24px 60px}
h1{font-size:24px;margin:0 0 4px} h2{font-size:18px;margin:36px 0 8px;border-top:1px solid #e6e5e0;padding-top:20px}
h3{font-size:15px;margin:22px 0 6px}
.sub{color:#52514e;margin:0 0 18px;font-size:13px}
.tiles{display:flex;flex-wrap:wrap;gap:12px;margin:12px 0}
.tile{border:1px solid #e6e5e0;border-radius:10px;padding:12px 16px;min-width:130px}
.tile b{display:block;font-size:24px} .tile span{color:#52514e;font-size:12px}
table{border-collapse:collapse;font-size:12.5px;margin:8px 0 4px;width:100%}
th,td{padding:5px 8px;border-bottom:1px solid #e6e5e0;text-align:right;white-space:nowrap}
th{color:#52514e;font-weight:600} th:first-child,td:first-child{text-align:left}
img{max-width:100%;display:block;margin:6px 0 4px}
.note{color:#52514e;font-size:12.5px;margin:4px 0 10px}
.won{color:#0a7d0a;font-weight:600} .lost{color:#b42f2f;font-weight:600}
"""


def build_report(sessions, out_dir):
    os.makedirs(out_dir, exist_ok=True)
    attempts = [a for s in sessions for a in s.attempts]
    images = {}

    def add(key, fig):
        images[key] = fig_to_data_uri(fig, os.path.join(out_dir, key + ".png"))
        return images[key]

    total_play = sum(session_times(s)[0] for s in sessions)
    reached = max((a.index for a in attempts), default=None)
    cleared = max((a.index for a in attempts if a.result == "won"), default=None)
    won = sum(1 for a in attempts if a.result == "won")
    finished = sum(1 for a in attempts if a.result in ("won", "lost"))
    players = {s.start.get("playerId") for s in sessions if s.start}

    out = [f"<!doctype html><html><head><meta charset='utf-8'><title>Ninja Spinner - gameplay report</title>"
           f"<style>{CSS}</style></head><body><main>",
           "<h1>Ninja Spinner - gameplay report</h1>",
           f"<p class='sub'>{len(sessions)} log file(s), {len(players)} player(s), {len(attempts)} level attempt(s)."
           " Charts are also saved as PNG files next to this report.</p>",
           "<div class='tiles'>"]
    for big, small in ((fmt_time(total_play), "time playing (m:ss)"), (str(len(attempts)), "level attempts"),
                       (f"{won}/{finished}" if finished else "–", "won / finished"),
                       (fmt(reached), "max level reached"), (fmt(cleared), "max level cleared")):
        out.append(f"<div class='tile'><b>{big}</b><span>{small}</span></div>")
    out.append("</div>")

    # Overview
    out.append("<h2>Overview</h2>")
    out.append(f"<img src='{add('overview_results', plot_results(attempts))}' alt='Attempts per level by result'>"
               if attempts else "<p class='note'>No level attempts in these logs yet.</p>")
    out.append(f"<img src='{add('overview_sessions', plot_sessions(sessions))}' alt='Session time'>")
    if any(a.summary and a.result in ("won", "lost") for a in attempts):
        out.append("<h3>Learning across attempts</h3>")
        out.append("<p class='note'>One point per attempt that ended in a win or a loss, oldest first "
                   "(restarted or abandoned attempts are left out). A dashed trend line appears from 3 attempts on. "
                   "Different levels differ in difficulty, so compare repeat attempts of the same level to "
                   "separate learning from level difficulty.</p>")
        out.append(f"<img src='{add('overview_learning', plot_learning(attempts))}' alt='Learning curves'>")

    # Attempts table
    out.append("<h3>All attempts</h3><table><tr><th>Attempt</th><th>File</th><th>Result</th><th>Time</th>"
               "<th>Stars</th><th>Hearts left</th><th>Kills</th><th>Powered saw</th><th>Flips/min</th>"
               "<th>Spin-up</th><th>Risk</th><th>In danger</th><th>Hits</th><th>Distance</th></tr>")
    for a in attempts:
        e, s = a.end or {}, a.summary or {}
        result = a.result + (f" ({e.get('reason')})" if e.get("reason") else "")
        cls = a.result if a.result in ("won", "lost") else ""
        out.append(
            f"<tr><td>{html.escape(a.label)}</td><td>{html.escape(a.session.name[8:23])}</td>"
            f"<td class='{cls}'>{html.escape(result)}</td><td>{fmt(e.get('elapsed'), 1, ' s')}</td>"
            f"<td>{fmt(e.get('stars'))}</td><td>{fmt(e.get('hearts'))}</td><td>{fmt(s.get('kills'))}</td>"
            f"<td>{fmt(s.get('poweredPct'), 0, ' %')}</td><td>{fmt(s.get('reversalsPerMin'), 1)}</td>"
            f"<td>{fmt(s.get('spinUp'), 1, ' s')}</td><td>{fmt(s.get('risk'))}</td>"
            f"<td>{fmt(s.get('dangerPct'), 0, ' %')}</td><td>{fmt(s.get('hits'))}</td>"
            f"<td>{fmt(s.get('distance'), 0, ' u')}</td></tr>")
    out.append("</table>")

    # Per attempt
    moves_by_level = defaultdict(list)
    for a in attempts:
        moves_by_level[a.scene].append(a.of("move"))
    for i, a in enumerate(attempts):
        key = f"attempt{i + 1:02d}_{a.scene}_{a.number}"
        e = a.end or {}
        out.append(f"<h2>{html.escape(a.label)} - {html.escape(a.result)}"
                   f"{' in ' + fmt(e.get('elapsed'), 1) + ' s' if e.get('elapsed') is not None else ''}</h2>")
        out.append(f"<p class='sub'>{html.escape(a.session.name)} · difficulty "
                   f"{html.escape(str(a.start.get('difficulty')))} · {len(a.enemies)} enemies</p>")
        if a.of("move"):
            out.append(f"<img src='{add(key + '_map', plot_map(a, moves_by_level[a.scene]))}' alt='Movement map'>")
            out.append(f"<img src='{add(key + '_risk', plot_risk(a))}' alt='Distance to enemies'>")
        if a.of("saw"):
            out.append(f"<img src='{add(key + '_saw', plot_saw(a))}' alt='Saw spin'>")
        kills = a.of("kill")
        if kills:
            out.append("<table><tr><th>Kill</th><th>At</th><th>Time to kill</th><th>Saw passes</th>"
                       "<th>Distance</th></tr>")
            for k in kills:
                out.append(f"<tr><td>{html.escape(str(k.get('enemyId')))}</td><td>{fmt(k.get('lt'), 1, ' s')}</td>"
                           f"<td>{fmt(k.get('timeToKill'), 1, ' s')}</td><td>{fmt(k.get('passes'))}</td>"
                           f"<td>{fmt(k.get('dist'), 1, ' u')}</td></tr>")
            out.append("</table>")
            missing = len(a.enemies) - len(kills) - int((e.get("enemiesLeft") or 0))
            if a.result == "won" and missing > 0:
                out.append(f"<p class='note'>{missing} kill(s) are missing from this log: it was written by the "
                           "logger version before the last-kill fix.</p>")

    out.append("</main></body></html>")
    path = os.path.join(out_dir, "report.html")
    with open(path, "w", encoding="utf-8") as f:
        f.write("\n".join(out))
    return path


def main():
    parser = argparse.ArgumentParser(description="Build an HTML report from Ninja Spinner gameplay logs.")
    parser.add_argument("paths", nargs="*", help="log folder(s) or session_*.log files (default: the game's log folder)")
    parser.add_argument("-o", "--out", help="output folder (default: <first log folder>/report)")
    parser.add_argument("--last", type=int, default=0, help="only use the N newest log files")
    parser.add_argument("--no-open", action="store_true", help="do not open the report in the browser")
    args = parser.parse_args()

    paths = args.paths or [default_log_folder()]
    files = find_logs(paths, args.last)
    if not files:
        sys.exit(f"No session_*.log files found in: {', '.join(paths)}")

    sessions = [read_session(f) for f in files]
    first = paths[0] if os.path.isdir(paths[0]) else os.path.dirname(os.path.abspath(files[0]))
    out_dir = args.out or os.path.join(first, "report")
    report = build_report(sessions, out_dir)
    attempts = sum(len(s.attempts) for s in sessions)
    print(f"Read {len(files)} log file(s), {attempts} level attempt(s).")
    print(f"Report: {os.path.abspath(report)}")
    if not args.no_open:
        webbrowser.open("file://" + os.path.abspath(report))


if __name__ == "__main__":
    main()
