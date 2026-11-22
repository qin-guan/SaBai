export type EditFormFields = {
  id: string;
  name: string;
  unitOfMeasurement: string;
  fallBelow: number;
  remarks?: string;
};

export type AddFormFields = Omit<EditFormFields, "id">;
