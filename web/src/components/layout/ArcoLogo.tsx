import { useId } from 'react'

export function ArcoLogo({ className }: { className?: string }) {
  const gradientId = useId()
  return (
    <svg viewBox="0 0 512 512" className={className} aria-hidden>
      <defs>
        <linearGradient id={gradientId} x1="0" y1="0" x2="1" y2="0">
          <stop offset="0" stopColor="#8b7cf6" />
          <stop offset="1" stopColor="#f6b44b" />
        </linearGradient>
      </defs>
      <rect width="512" height="512" rx="112" fill="#0f1117" />
      <path
        d="M116 330 A140 140 0 0 1 396 330"
        fill="none"
        stroke={`url(#${gradientId})`}
        strokeWidth="60"
        strokeLinecap="round"
      />
      <circle cx="256" cy="322" r="30" fill="#f6b44b" />
    </svg>
  )
}
