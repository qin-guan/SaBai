import { z } from "zod";

export const brandFormSchema = z.object({
  name: z.string().trim().min(1, "Name is required").max(255),
  activeIngredientId: z.string().min(1, "Missing Active Ingredient!"),
  remarks: z.string().trim().optional(),
});

export const editBrandFormSchema = brandFormSchema.extend({
  id: z.string().min(1),
});

export type AddFormFields = z.infer<typeof brandFormSchema>;
export type EditFormFields = z.infer<typeof editBrandFormSchema>;
