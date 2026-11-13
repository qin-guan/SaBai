import { useQuery } from "@tanstack/vue-query";

/** All known freeform tags across staff users and patients, for autocomplete. */
export function useTags() {
  return useQuery({
    queryKey: queryKeys.tags(),
    queryFn: ({ signal }) =>
      $fetch<{ tags: string[] }>("/api/tags", {
        credentials: "same-origin",
        signal,
      }).then((x) => x.tags),
  });
}
