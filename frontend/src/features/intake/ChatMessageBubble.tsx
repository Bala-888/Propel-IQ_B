import type { ChatMessage } from '../../api/intakeAiApi'

interface ChatMessageBubbleProps {
  message: ChatMessage
}

// ── Inline SVG icons ──────────────────────────────────────────────────────────────────────────────

/** Star/sparkle icon used in the AI confidence badge (UXR-101; UXR-105 — icon+text, not colour-only). */
function SparkleIcon() {
  return (
    <svg
      aria-hidden="true"
      width="11"
      height="11"
      viewBox="0 0 16 16"
      fill="currentColor"
      xmlns="http://www.w3.org/2000/svg"
    >
      <path d="M8 0l1.5 5.5L15 7l-5.5 1.5L8 16l-1.5-5.5L1 9l5.5-1.5L8 0z" />
    </svg>
  )
}

/**
 * AI confidence badge (UXR-101).
 * Combines icon + text label — colour is supplementary, never the sole indicator
 * of confidence level (UXR-105; WCAG 1.4.1).
 */
function ConfidenceBadge() {
  return (
    <div
      style={{
        display: 'inline-flex',
        alignItems: 'center',
        gap: '4px',
        background: 'var(--color-ai-accent-surface)',
        color: 'var(--color-ai-accent)',
        fontSize: '11px',
        fontWeight: 600,
        padding: '2px 8px',
        borderRadius: 'var(--radius-full)',
        border: '1px solid #C7D2FE',
        marginBottom: 'var(--space-2)',
      }}
      aria-label="AI-generated content"
    >
      <SparkleIcon />
      <span>AI suggestion</span>
    </div>
  )
}

/**
 * Renders a single chat message bubble.
 *
 * - AI messages: left-aligned, indigo avatar, confidence badge above the bubble (UXR-101).
 * - Patient messages: right-aligned, blue avatar, primary-coloured bubble.
 *
 * Chat messages are never stored in any browser-persistent storage — they exist only in
 * component state scoped to the session (AIR guardrails; OWASP A02).
 */
export function ChatMessageBubble({ message }: ChatMessageBubbleProps) {
  const isAi = message.role === 'ai'

  return (
    <div
      style={{
        display: 'flex',
        gap: 'var(--space-3)',
        maxWidth: '80%',
        alignSelf: isAi ? 'flex-start' : 'flex-end',
        flexDirection: isAi ? 'row' : 'row-reverse',
      }}
    >
      {/* Avatar */}
      <div
        aria-hidden={isAi}
        aria-label={isAi ? undefined : 'You'}
        style={{
          width: 32,
          height: 32,
          borderRadius: '50%',
          flexShrink: 0,
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
          fontSize: '11px',
          fontWeight: 700,
          background: isAi ? 'var(--color-ai-accent-surface)' : 'var(--color-primary-subtle)',
          color: isAi ? 'var(--color-ai-accent)' : 'var(--color-primary)',
        }}
      >
        {isAi ? 'AI' : 'P'}
      </div>

      {/* Bubble + optional confidence badge */}
      <div style={{ display: 'flex', flexDirection: 'column', alignItems: isAi ? 'flex-start' : 'flex-end' }}>
        {/* UXR-101: confidence badge on every AI message — icon + text always present together */}
        {isAi && <ConfidenceBadge />}

        <div
          style={{
            padding: 'var(--space-3) var(--space-4)',
            borderRadius: 'var(--radius-md)',
            fontSize: '15px',
            lineHeight: 1.5,
            background: isAi ? 'var(--color-bg-surface)' : 'var(--color-primary)',
            color: isAi ? 'var(--color-text-primary)' : 'var(--color-text-inverse)',
            border: isAi ? '1px solid var(--color-border)' : 'none',
            boxShadow: 'var(--shadow-1)',
            whiteSpace: 'pre-wrap',
            wordBreak: 'break-word',
          }}
        >
          {message.content}
        </div>
      </div>
    </div>
  )
}
