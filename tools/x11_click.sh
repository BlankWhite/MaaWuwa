#!/usr/bin/env bash
set -euo pipefail

# Click an X11/Xwayland coordinate using XTest.
# Usage:
#   x11_click.sh '[x,y,w,h]'        # clicks center of Maa BOX
#   x11_click.sh x y                # clicks fixed coordinate
#   x11_click.sh --down x y         # mouse button down at coordinate
#   x11_click.sh --up x y           # mouse button up at coordinate
# Env:
#   MAAWUWA_DISPLAY=:1              # default Xwayland display

if [[ -n "${MAAWUWA_DISPLAY:-}" ]]; then
    DISPLAY_NAME="$MAAWUWA_DISPLAY"
else
    # Nested labwc usually starts a dedicated Xwayland display (:1). Pick the
    # highest non-:0 X11 socket to avoid the outer desktop's Xwayland (:0).
    DISPLAY_NAME=":1"
    for sock in /tmp/.X11-unix/X*; do
        [[ -e "$sock" ]] || continue
        n="${sock##*/X}"
        [[ "$n" =~ ^[0-9]+$ ]] || continue
        if (( n > 0 )); then
            DISPLAY_NAME=":$n"
        fi
    done
fi

mode="click"
if [[ $# -ge 1 && ( "$1" == "--down" || "$1" == "--up" ) ]]; then
    mode="${1#--}"
    shift
fi

HELPER="${TMPDIR:-/tmp}/maawuwa-x11-click"
SRC="${TMPDIR:-/tmp}/maawuwa-x11-click.c"

if [[ ! -x "$HELPER" ]] || ! "$HELPER" --version 2>/dev/null | grep -q '^maawuwa-x11-click 2$'; then
    cat >"$SRC" <<'C'
#include <X11/Xlib.h>
#include <X11/extensions/XTest.h>
#include <stdio.h>
#include <stdlib.h>
#include <unistd.h>
#include <string.h>

int main(int argc, char **argv) {
    if (argc == 2 && strcmp(argv[1], "--version") == 0) {
        puts("maawuwa-x11-click 2");
        return 0;
    }
    if (argc != 5) {
        fprintf(stderr, "Usage: %s DISPLAY MODE X Y\n", argv[0]);
        return 2;
    }
    const char *display_name = argv[1];
    const char *mode = argv[2];
    int x = atoi(argv[3]);
    int y = atoi(argv[4]);

    Display *dpy = XOpenDisplay(display_name);
    if (!dpy) {
        fprintf(stderr, "Failed to open display: %s\n", display_name);
        return 1;
    }

    int event_base = 0, error_base = 0, major = 0, minor = 0;
    if (!XTestQueryExtension(dpy, &event_base, &error_base, &major, &minor)) {
        fprintf(stderr, "XTest extension is not available on display: %s\n", display_name);
        XCloseDisplay(dpy);
        return 1;
    }

    Window root = DefaultRootWindow(dpy);
    XWarpPointer(dpy, None, root, 0, 0, 0, 0, x, y);
    XFlush(dpy);
    usleep(50000);

    if (strcmp(mode, "down") == 0) {
        XTestFakeButtonEvent(dpy, 1, True, CurrentTime);
        XFlush(dpy);
        usleep(50000);
    } else if (strcmp(mode, "up") == 0) {
        XTestFakeButtonEvent(dpy, 1, False, CurrentTime);
        XFlush(dpy);
        usleep(50000);
    } else if (strcmp(mode, "click") == 0) {
        XTestFakeButtonEvent(dpy, 1, True, CurrentTime);
        XFlush(dpy);
        usleep(120000);
        XTestFakeButtonEvent(dpy, 1, False, CurrentTime);
        XFlush(dpy);
        usleep(50000);
    } else {
        fprintf(stderr, "Unknown mode: %s\n", mode);
        XCloseDisplay(dpy);
        return 2;
    }

    XCloseDisplay(dpy);
    return 0;
}
C
    gcc "$SRC" -o "$HELPER" -lXtst -lX11
fi

if [[ $# -eq 1 ]]; then
    # Maa {BOX}: [x, y, w, h], tolerate spaces.
    nums=$(printf '%s' "$1" | tr -d '[]' | tr ',' ' ')
    # shellcheck disable=SC2086
    read -r x y w h <<< "$nums"
    if [[ -z "${x:-}" || -z "${y:-}" || -z "${w:-}" || -z "${h:-}" ]]; then
        echo "Invalid BOX: $1" >&2
        exit 2
    fi
    cx=$((x + w / 2))
    cy=$((y + h / 2))
elif [[ $# -eq 2 ]]; then
    cx="$1"
    cy="$2"
else
    echo "Usage: $0 [--down|--up] '[x,y,w,h]' OR $0 [--down|--up] x y" >&2
    exit 2
fi

printf '[%s] display=%s mode=%s args=%q %q -> click=(%s,%s)\n' \
    "$(date '+%F %T')" "$DISPLAY_NAME" "$mode" "${1:-}" "${2:-}" "$cx" "$cy" \
    >> "${TMPDIR:-/tmp}/maawuwa-x11-click.log"

"$HELPER" "$DISPLAY_NAME" "$mode" "$cx" "$cy"
