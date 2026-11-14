import { useMutation, useQuery, useQueryClient } from '@tanstack/vue-query'
import {
  saBaiAPIEndpointsBroadcastsCreatePostEndpoint,
  saBaiAPIEndpointsBroadcastsDeleteDeleteEndpoint,
  saBaiAPIEndpointsBroadcastsListGetEndpoint,
  saBaiAPIEndpointsBroadcastsPublishPostEndpoint,
} from '@sabai/sdk/clients'
import type {
  SaBaiAPIDomainBroadcastSeverityKey,
  SaBaiAPIEndpointsBroadcastsListGetResponse,
} from '@sabai/sdk/types'

export type BroadcastItem = SaBaiAPIEndpointsBroadcastsListGetResponse

export interface SendBroadcastInput {
  title: string
  body: string
  severity?: SaBaiAPIDomainBroadcastSeverityKey
}

/**
 * Past broadcasts and clinic notifications, newest first.
 */
export function useBroadcastsList() {
  return useQuery({
    queryKey: queryKeys.broadcasts(),
    queryFn: ({ signal }) =>
      saBaiAPIEndpointsBroadcastsListGetEndpoint({
        ...apiConfig,
        signal,
      }).unwrap(),
  })
}

/**
 * Creates and immediately publishes a broadcast, sending Web Push notifications
 * to all subscribed staff devices when server push is configured.
 */
export function useSendBroadcast() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (input: SendBroadcastInput) => {
      const created = await saBaiAPIEndpointsBroadcastsCreatePostEndpoint({
        ...apiConfig,
        body: {
          title: input.title.trim(),
          body: input.body.trim(),
          severity: input.severity ?? 'info',
        },
        throwOnError: true,
      }).unwrap()

      const published = await saBaiAPIEndpointsBroadcastsPublishPostEndpoint({
        ...apiConfig,
        path: { id: created.id },
        throwOnError: true,
      }).unwrap()

      return published
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.broadcasts() })
    },
  })
}

/**
 * Deletes a broadcast from history.
 */
export function useDeleteBroadcast() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id: string) =>
      saBaiAPIEndpointsBroadcastsDeleteDeleteEndpoint({
        ...apiConfig,
        path: { id },
        throwOnError: true,
      }).unwrap(),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.broadcasts() })
    },
  })
}

/**
 * Publishes an existing unpublished broadcast.
 */
export function usePublishBroadcast() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id: string) =>
      saBaiAPIEndpointsBroadcastsPublishPostEndpoint({
        ...apiConfig,
        path: { id },
        throwOnError: true,
      }).unwrap(),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.broadcasts() })
    },
  })
}
