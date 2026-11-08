import type ns from '../pt-BR/push'

const push_en: typeof ns = {
  state: {
    on: 'Enabled on this device.',
    off: 'Disabled on this device.',
    denied: 'Blocked by the browser. Allow Arco notifications in your browser settings.',
    noSw: "Available only in the published app (development mode doesn't register the service worker).",
    needsInstall: 'On iPhone/iPad, install Arco on your home screen (Share → Add to Home Screen) and open it from there.',
    unsupported: "This browser doesn't support push notifications.",
  },
  disabledToast: 'Notifications disabled on this device.',
  enabledToast: 'Notifications enabled.',
  testSent: 'Test sent to {{count}} device',
  testSentMany: 'Test sent to {{count}} devices',
  testNone: 'No device received the test.',
  devices: '{{count}} device subscribed.',
  devicesMany: '{{count}} devices subscribed.',
  disable: 'Disable',
  enable: 'Enable notifications',
  sendTest: 'Send test',
  reminderHint: "Each habit's reminder time is set when editing the habit.",
  reminderSaved: 'Review reminder saved.',
  weeklyReminder: 'Weekly review reminder',
  day: 'Day',
  time: 'Time',
  timeInvalid: 'Use HH:mm.',
  saveReminder: 'Save reminder',
}
export default push_en
