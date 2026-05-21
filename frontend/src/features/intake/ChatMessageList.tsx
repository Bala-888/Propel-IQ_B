import { useEffect, useRef } from 'react'
import type { ChatMessage } from '../../api/intakeAiApi'
import { ChatMessageBubble } from './ChatMessageBubble'

interface ChatMessageListProps {
  messages: ChatMessage[]
  isLoading: boolean
}

/** Animated dots shown while the AI is generating a response (UXR-504). */
function TypingIndicator() {
  return (
    <div
      style={{
        display: 'flex',
        gap: 'var(--space-3)',
        maxWidth: '80%',
        alignSelf: 'flex-start',
      }}
      aria-label="AI is generating a response"
      role="status"
    >
      {/* AI avatar */}
      <div
        aria-hidden="true"
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
          background: 'var(--color-ai-accent-surface)',
          color: 'var(--color-ai-accent)',
        }}
      >
        AI
      </div>
      <div
        style={{
          padding: 'var(--space-3) var(--space-4)',
          borderRadius: 'var(--radius-md)',
          background: 'var(--color-bg-surface)',
          border: '1px solid var(--color-border)',
          boxShadow: 'var(--shadow-1)',
          display: 'flex',
          alignItems: 'center',
          gap: '5px',
        }}
      >
        {[0, 1, 2].map(i => (
          <span
            key={i}
            style={{
              width: 8,
              height: 8,
              borderRadius: '50%',
              background: 'var(--color-text-disabled)',
              display: 'inline-block',
              animation: 'ai-typing-pulse 1.4s ease-in-out infinite',
              animationDelay: `${i * 0.2}s`,
            }}
          />
        ))}
      </div>
    </div>
  )
}

/**
 * Scrollable list of chat message bubbles with auto-scroll to the latest message.
 *
 * Uses a bottom-sentinel `<div ref={bottomRef}>` with `scrollIntoView({ behavior: 'smooth' })`
 * called inside `useEffect([messages, isLoading])` — scroll fires after every React render cycle
 * that adds a message or starts/stops loading (AC-001, AC-002; checklist).
 *
 * The `role="log"` + `aria-live="polite"` attributes announce new AI messages to screen readers
 * without interrupting the patient while they type (WAI-ARIA chat log pattern).
 */
export function ChatMessageList({ messages, isLoading }: ChatMessageListProps) {
  const bottomRef = useRef<HTMLDivElement>(null)

  // Auto-scroll to bottom sentinel after every message push or loading state change (checklist)
  useEffect(() => {
    bottomRef.current?.scrollIntoView({ behavior: 'smooth' })
  }, [messages, isLoading])

  return (
    <>
      {/* Keyframe animation for typing dots — injected once into the document head */}
      <style>{`
        @keyframes ai-typing-pulse {
          0%, 60%, 100% { transform: translateY(0); opacity: 0.5; }
          30% { transform: translateY(-5px); opacity: 1; }
        }
      `}</style>

      <div
        role="log"
        aria-live="polite"
        aria-label="Conversation history"
        style={{
          flex: 1,
          overflowY: 'auto',
          padding: 'var(--space-6)',
          display: 'flex',
          flexDirection: 'column',
          gap: 'var(--space-5)',
        }}
      >
        {messages.map(msg => (
          <ChatMessageBubble key={msg.id} message={msg} />
        ))}

        {/* Typing indicator shown while awaiting an AI reply (UXR-504) */}
        {isLoading && <TypingIndicator />}

        {/* Bottom sentinel — scrollIntoView target (checklist: auto-scroll) */}
        <div ref={bottomRef} aria-hidden="true" />
      </div>
    </>
  )
}
