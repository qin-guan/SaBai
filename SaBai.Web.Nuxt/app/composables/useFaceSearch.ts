import { useQuery } from "@tanstack/vue-query";
import type { MaybeRefOrGetter } from "vue";
import { saBaiAPIEndpointsPatientsSearchByFacePostEndpoint } from "@sabai/sdk/clients";

/** Searches the face index for patients matching a captured photo (data URL). */
export function useFaceSearch(image: MaybeRefOrGetter<string | null>, maxMatches = 5) {
  return useQuery({
    queryKey: computed(() => queryKeys.faceSearch(toValue(image) ?? "")),
    enabled: computed(() => !!toValue(image)),
    retry: false,
    queryFn: async () => {
      const file = await dataUrlToFile(toValue(image)!, "face-search");
      const result = await saBaiAPIEndpointsPatientsSearchByFacePostEndpoint({
        ...apiConfig,
        body: { image: file },
        query: { maxMatches },
        throwOnError: true,
      }).unwrap();
      return result.matches;
    },
  });
}
