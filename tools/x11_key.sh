#!/usr/bin/env bash
set -euo pipefail

# Send an X11/Xwayland key using XTest.
# Usage:
#   x11_key.sh Escape
#   x11_key.sh Esc
#   x11_key.sh Return
#   x11_key.sh Space
#   x11_key.sh --down w
#   x11_key.sh --up w
# Env:
#   MAAWUWA_DISPLAY=:1         # override Xwayland display

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
if [[ $# -eq 2 && ( "$1" == "--down" || "$1" == "--up" ) ]]; then
    mode="${1#--}"
    key="$2"
elif [[ $# -eq 1 ]]; then
    key="$1"
else
    echo "Usage: $0 [--down|--up] KEY" >&2
    exit 2
fi
case "${key,,}" in
    esc|escape) key="Escape" ;;
    enter|return) key="Return" ;;
    space) key="space" ;;
esac

HELPER="${TMPDIR:-/tmp}/maawuwa-x11-key"
SRC="${TMPDIR:-/tmp}/maawuwa-x11-key.c"

if [[ ! -x "$HELPER" ]] || ! "$HELPER" --version 2>/dev/null | grep -q '^maawuwa-x11-key 2$'; then
    cat >"$SRC" <<'C'
#include <X11/Xlib.h>
#include <X11/keysym.h>
#include <X11/extensions/XTest.h>
#include <stdio.h>
#include <stdlib.h>
#include <unistd.h>
#include <string.h>

int main(int argc, char **argv) {
    if (argc == 2 && strcmp(argv[1], "--version") == 0) {
        puts("maawuwa-x11-key 2");
        return 0;
    }
    if (argc != 4) {
        fprintf(stderr, "Usage: %s DISPLAY MODE KEY\n", argv[0]);
        return 2;
    }
    const char *display_name = argv[1];
    const char *mode = argv[2];
    const char *key_name = argv[3];

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

    KeySym keysym = XStringToKeysym(key_name);
    if (keysym == NoSymbol) {
        fprintf(stderr, "Unknown key: %s\n", key_name);
        XCloseDisplay(dpy);
        return 2;
    }

    KeyCode keycode = XKeysymToKeycode(dpy, keysym);
    if (keycode == 0) {
        fprintf(stderr, "No keycode for key: %s\n", key_name);
        XCloseDisplay(dpy);
        return 2;
    }

    if (strcmp(mode, "down") == 0) {
        XTestFakeKeyEvent(dpy, keycode, True, CurrentTime);
        XFlush(dpy);
        usleep(50000);
    } else if (strcmp(mode, "up") == 0) {
        XTestFakeKeyEvent(dpy, keycode, False, CurrentTime);
        XFlush(dpy);
        usleep(50000);
    } else if (strcmp(mode, "click") == 0) {
        XTestFakeKeyEvent(dpy, keycode, True, CurrentTime);
        XFlush(dpy);
        usleep(80000);
        XTestFakeKeyEvent(dpy, keycode, False, CurrentTime);
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

printf '[%s] display=%s mode=%s key=%q -> %s\n' \
    "$(date '+%F %T')" "$DISPLAY_NAME" "$mode" "$key" "$key" \
    >> "${TMPDIR:-/tmp}/maawuwa-x11-key.log"

"$HELPER" "$DISPLAY_NAME" "$mode" "$key"
