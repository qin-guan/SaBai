import { useQuery, useQueryClient } from '@tanstack/vue-query'

/** This device's push subscription, or null when it has none or cannot have one. */
async function currentSubscription(): Promise<PushSubscription | null> {
  if (!('serviceWorker' in navigator) || !('PushManager' in window))
    return null
  const registration = await navigator.serviceWorker.getRegistration()
  return (await registration?.pushManager.getSubscription()) ?? null
}

/** The active service worker, or an error if it has not started within a few seconds. */
function serviceWorkerReady(): Promise<ServiceWorkerRegistration> {
  return Promise.race([
    navigator.serviceWorker.ready,
    new Promise<never>((_, reject) =>
      setTimeout(() => reject(new Error('The app is still starting. Reload the page and try again.')), 8000)),
  ])
}

/**
 * Stops notifications on this device for the signed-in user: forgets the
 * subscription on the server, then in the browser. Returns false if the
 * server could not be told (the browser subscription is then kept, so the
 * two stay in step).
 */
export async function stopPushOnThisDevice(): Promise<boolean> {
  const subscription = await currentSubscription()
  if (!subscription)
    return true
  try {
    await removePushSubscription(subscription.endpoint)
  }
  catch (error) {
    console.error(error)
    return false
  }
  await subscription.unsubscribe()
  return true
}

/**
 * Notifications on this device for the signed-in user: whether they can work
 * here (`status`), and turning them on, off, or sending a test.
 */
export function usePushNotifications() {
  const queryClient = useQueryClient()
  const config = useQuery({
    queryKey: queryKeys.pushConfig(),
    queryFn: ({ signal }) => getPushConfig(signal),
  })

  const hasPushApi = 'serviceWorker' in navigator && 'PushManager' in window && 'Notification' in window
  const isStandalone = window.matchMedia('(display-mode: standalone)').matches
    || (navigator as Navigator & { standalone?: boolean }).standalone === true
  const permission = ref<NotificationPermission>(hasPushApi ? Notification.permission : 'default')
  const isSubscribed = ref(false)
  /** False until the browser has been asked whether this device is subscribed. */
  const isChecked = ref(false)

  const status = computed(() => pushStatus({
    hasPushApi,
    isIos: isIosDevice(navigator.userAgent, navigator.maxTouchPoints),
    isStandalone,
    permission: permission.value,
    serverEnabled: config.data.value?.isEnabled ?? false,
    isSubscribed: isSubscribed.value,
  }))

  const refreshConfig = () => queryClient.invalidateQueries({ queryKey: queryKeys.pushConfig() })

  onMounted(async () => {
    try {
      const subscription = await currentSubscription()
      isSubscribed.value = !!subscription
      // The browser can replace a subscription on its own, and another user may
      // have used this device since: make the server match what is here now.
      if (subscription && permission.value === 'granted' && (await config.suspense()).data?.isEnabled) {
        await savePushSubscription(subscription)
        await refreshConfig()
      }
    }
    catch (error) {
      console.error(error)
    }
    finally {
      isChecked.value = true
    }
  })

  /** Asks for permission and subscribes. Call it straight from a tap: iOS requires that. */
  async function turnOn() {
    const publicKey = config.data.value?.publicKey
    if (!publicKey)
      throw new Error('Notifications are not set up on this server.')

    permission.value = await Notification.requestPermission()
    if (permission.value !== 'granted')
      return

    const registration = await serviceWorkerReady()
    const options = { userVisibleOnly: true, applicationServerKey: base64UrlToBytes(publicKey) }
    let subscription: PushSubscription
    try {
      subscription = await registration.pushManager.subscribe(options)
    }
    catch (error) {
      // A subscription made with an older server key has to be dropped first.
      const existing = await registration.pushManager.getSubscription()
      if (!existing)
        throw error
      await existing.unsubscribe()
      subscription = await registration.pushManager.subscribe(options)
    }

    try {
      await savePushSubscription(subscription)
    }
    catch (error) {
      // Not saved, so nothing would ever arrive: don't leave the device looking subscribed.
      await subscription.unsubscribe()
      throw error
    }
    isSubscribed.value = true
    await refreshConfig()
  }

  async function turnOff() {
    if (!(await stopPushOnThisDevice()))
      throw new Error('Unable to reach the server. Notifications are still on.')
    isSubscribed.value = false
    await refreshConfig()
  }

  return { config, status, isChecked, turnOn, turnOff, sendTest: sendTestPush }
}
