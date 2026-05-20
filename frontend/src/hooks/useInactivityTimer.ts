import { useCallback, useEffect, useRef } from 'react'

export interface InactivityTimerOptions {
  /** Milliseconds of inactivity before onWarning fires (default usage: 14 * 60 * 1000). */
  warningAtMs: number
  /** Milliseconds of inactivity before onTimeout fires (default usage: 15 * 60 * 1000). */
  timeoutAtMs: number
  /** Called when the warning threshold is reached (e.g. open the session-timeout modal). */
  onWarning: () => void
  /** Called when the timeout threshold is reached (e.g. forced sign-out). */
  onTimeout: () => void
  /**
   * Optional: called when a cross-tab SESSION_EXTENDED BroadcastChannel message is received.
   * Allows the receiving tab to close an open warning modal without the user clicking "Stay signed in".
   * (Edge: multiple tabs — timer reset propagates to all tabs via BroadcastChannel.)
   */
  onExternalExtend?: () => void
}

const ACTIVITY_EVENTS = ['mousemove', 'keydown', 'mousedown', 'touchstart', 'scroll'] as const

/**
 * Listens for DOM activity events and fires `onWarning` / `onTimeout` callbacks after the
 * configured idle periods. Exposes a `reset()` function for manual timer restart (e.g. after
 * a successful "Stay signed in" refresh).
 *
 * BroadcastChannel('upacip-session') is subscribed so that a SESSION_EXTENDED message posted
 * by any tab resets the inactivity clock here too (Edge: multiple tabs).
 *
 * All callbacks are stored in refs — the effect runs once and never re-fires due to
 * callback identity changes (us_010/AC-001, AC-003).
 */
export function useInactivityTimer({
  warningAtMs,
  timeoutAtMs,
  onWarning,
  onTimeout,
  onExternalExtend,
}: InactivityTimerOptions): { reset: () => void } {
  const warningTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null)
  const timeoutTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null)

  // Ref-backed callbacks — reads the latest closure without re-running the effect.
  const onWarningRef = useRef(onWarning)
  const onTimeoutRef = useRef(onTimeout)
  const onExternalExtendRef = useRef(onExternalExtend)
  const warningAtMsRef = useRef(warningAtMs)
  const timeoutAtMsRef = useRef(timeoutAtMs)

  useEffect(() => { onWarningRef.current = onWarning }, [onWarning])
  useEffect(() => { onTimeoutRef.current = onTimeout }, [onTimeout])
  useEffect(() => { onExternalExtendRef.current = onExternalExtend }, [onExternalExtend])
  useEffect(() => { warningAtMsRef.current = warningAtMs }, [warningAtMs])
  useEffect(() => { timeoutAtMsRef.current = timeoutAtMs }, [timeoutAtMs])

  /**
   * Clears both timers and re-starts them from the current ref values.
   * Stable reference (empty deps) — safe to pass to addEventListener and BroadcastChannel.
   */
  const resetTimer = useCallback(() => {
    if (warningTimerRef.current !== null) clearTimeout(warningTimerRef.current)
    if (timeoutTimerRef.current !== null) clearTimeout(timeoutTimerRef.current)
    warningTimerRef.current = setTimeout(() => onWarningRef.current(), warningAtMsRef.current)
    timeoutTimerRef.current = setTimeout(() => onTimeoutRef.current(), timeoutAtMsRef.current)
  }, []) // stable — all reads go through refs

  useEffect(() => {
    // BroadcastChannel: instantiated once per mount; closed in cleanup (resource management).
    const channel = new BroadcastChannel('upacip-session')
    channel.onmessage = (e: MessageEvent<{ type: string }>) => {
      if (e.data?.type === 'SESSION_EXTENDED') {
        resetTimer()
        onExternalExtendRef.current?.()
      }
    }

    // Passive listeners — does not block scroll thread (Edge: activity detection; browser performance).
    ACTIVITY_EVENTS.forEach((ev) => window.addEventListener(ev, resetTimer, { passive: true }))

    resetTimer() // start timers on mount

    return () => {
      ACTIVITY_EVENTS.forEach((ev) => window.removeEventListener(ev, resetTimer))
      if (warningTimerRef.current !== null) clearTimeout(warningTimerRef.current)
      if (timeoutTimerRef.current !== null) clearTimeout(timeoutTimerRef.current)
      channel.close()
    }
  }, [resetTimer]) // resetTimer is stable so this runs exactly once

  return { reset: resetTimer }
}
