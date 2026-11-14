import { useQuery } from "@tanstack/vue-query";
import { fetchTags } from "~/offline/mutations/patients";

/** All known freeform tags across staff users and patients, for autocomplete. */
export function useTags() {
  return useQuery({
    queryKey: queryKeys.tags(),
    queryFn: ({ signal }) => fetchTags(signal),
  });
}
