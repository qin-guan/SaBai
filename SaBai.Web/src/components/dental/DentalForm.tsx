import { ReactNode, useEffect } from "react";
import {
  Controller,
  FormProvider,
  useFieldArray,
  useForm,
  useFormContext,
} from "react-hook-form";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  saBaiAPIEndpointsDentalGetByVisitGetEndpointQueryKey,
  saBaiAPIEndpointsDentalGetByVisitGetEndpointQueryOptions,
  saBaiAPIEndpointsDentalUpsertByVisitPutEndpointMutationOptions,
} from "@sabai/sdk/hooks";
import type {
  SaBaiAPIEndpointsDentalDentalConsultResponse,
  SaBaiAPIEndpointsDentalUpsertByVisitPutRequest,
} from "@sabai/sdk/types";
import { clsx } from "clsx";
import toast from "react-hot-toast";
import { apiConfig } from "@/lib/api/config";
import { getApiErrorMessage } from "@/lib/api/errors";
import LoadingSpinner from "@/components/LoadingSpinner";
import { Button } from "@/components/interactive/Button/Button";
import {
  BRUSHING_FREQUENCY,
  EXTRAORAL_EXAM,
  HISTORY_YES_NO,
  HISTORY_YES_NO_UNKNOWN,
  INTRAORAL_EXAM,
  Option,
  REFERRAL_PRIORITY,
  REFERRED_FOR,
  SUGAR_CONSUMPTION,
  TREATMENTS,
  TREATMENT_STATUS,
  YES_NO,
  YES_NO_UNKNOWN,
} from "./dentalOptions";

type TreatmentKey = (typeof TREATMENTS)[number]["key"];
type YesNoName = (typeof HISTORY_YES_NO)[number]["name"];
type ChoiceName =
  | (typeof HISTORY_YES_NO_UNKNOWN)[number]["name"]
  | (typeof EXTRAORAL_EXAM)[number]["name"]
  | (typeof INTRAORAL_EXAM)[number]["name"]
  | "brushingFrequency"
  | "sugarConsumption"
  | "referredFor"
  | "referralPriority";
type TextName =
  | "otherDentalHistory"
  | "chiefComplaint"
  | "referralNotes"
  | "recommendedFacility"
  | "remarks";

/**
 * Every answer is held as a string so chips, selects and inputs share one shape;
 * "" means unanswered and is sent to the API as null.
 */
export type DentalFormValues = Record<
  YesNoName | ChoiceName | TextName,
  string
> &
  Record<
    `${TreatmentKey}${"Done" | "Date" | "Operator" | "ToothNumbers"}`,
    string
  > & { diagnoses: { value: string }[] };

type Request = SaBaiAPIEndpointsDentalUpsertByVisitPutRequest;
type Response = SaBaiAPIEndpointsDentalDentalConsultResponse;

const YES_NO_NAMES = HISTORY_YES_NO.map((q) => q.name);
const CHOICE_NAMES: ChoiceName[] = [
  ...HISTORY_YES_NO_UNKNOWN.map((q) => q.name),
  ...EXTRAORAL_EXAM.map((q) => q.name),
  ...INTRAORAL_EXAM.map((q) => q.name),
  "brushingFrequency",
  "sugarConsumption",
  "referredFor",
  "referralPriority",
];
const TEXT_NAMES: TextName[] = [
  "otherDentalHistory",
  "chiefComplaint",
  "referralNotes",
  "recommendedFacility",
  "remarks",
];

const fromBool = (
  value: boolean | null | undefined,
  yes: string,
  no: string,
) => (value === true ? yes : value === false ? no : "");

const toBool = (value: string, yes: string) =>
  value === "" ? null : value === yes;

export function toFormValues(data?: Response | null): DentalFormValues {
  const record = (data ?? {}) as Record<string, unknown>;
  const values: Record<string, unknown> = {};
  for (const name of YES_NO_NAMES) {
    values[name] = fromBool(record[name] as boolean | null, "yes", "no");
  }
  for (const name of [...CHOICE_NAMES, ...TEXT_NAMES]) {
    values[name] = (record[name] as string | null | undefined) ?? "";
  }
  for (const { key } of TREATMENTS) {
    values[`${key}Done`] = fromBool(
      record[`${key}Done`] as boolean | null,
      "done",
      "notDone",
    );
    for (const field of ["Date", "Operator", "ToothNumbers"]) {
      values[`${key}${field}`] =
        (record[`${key}${field}`] as string | null | undefined) ?? "";
    }
  }
  values.diagnoses = ((data?.diagnoses as string[] | undefined) ?? []).map(
    (value) => ({ value }),
  );
  return values as DentalFormValues;
}

export function toRequest(values: DentalFormValues): Request {
  const body: Record<string, unknown> = {};
  for (const name of YES_NO_NAMES) {
    body[name] = toBool(values[name], "yes");
  }
  for (const name of [...CHOICE_NAMES, ...TEXT_NAMES]) {
    body[name] = values[name].trim() || null;
  }
  for (const { key } of TREATMENTS) {
    body[`${key}Done`] = toBool(values[`${key}Done`], "done");
    for (const field of ["Date", "Operator", "ToothNumbers"] as const) {
      body[`${key}${field}`] = values[`${key}${field}`].trim() || null;
    }
  }
  body.diagnoses = values.diagnoses
    .map((d) => d.value.trim())
    .filter((d) => d.length > 0);
  return body as Request;
}

const SECTIONS = [
  { id: "dental-history", title: "Dental History", tab: "Dental History" },
  { id: "chief-complaint", title: "Chief Complaint", tab: null },
  {
    id: "clinical-examination",
    title: "Clinical Examination",
    tab: "Clinical Examination",
  },
  { id: "dental-diagnosis", title: "Dental Diagnosis", tab: "Diagnosis" },
  {
    id: "treatment-provided",
    title: "Treatment Provided",
    tab: "Treatment Provided",
  },
  { id: "referral", title: "Referral", tab: "Referral" },
  { id: "remarks", title: "Remarks", tab: "Remarks" },
] as const;

const inputClass =
  "w-full rounded-md border border-slate-300 bg-white px-3 py-2 text-sm text-slate-900 placeholder:text-slate-400 focus:border-indigo-500 focus:outline-none focus:ring-2 focus:ring-indigo-200";

function Card({ index, children }: { index: number; children: ReactNode }) {
  const section = SECTIONS[index];
  return (
    <section
      id={section.id}
      aria-labelledby={`${section.id}-title`}
      className="scroll-mt-24 rounded-xl border border-slate-200 bg-white p-5 shadow-sm"
    >
      <h2
        id={`${section.id}-title`}
        className="mb-4 flex items-center gap-2 text-base font-semibold text-slate-900"
      >
        <span className="flex h-6 w-6 items-center justify-center rounded-full bg-indigo-900 text-xs text-white">
          {index + 1}
        </span>
        {section.title}
      </h2>
      {children}
    </section>
  );
}

function FieldLabel({
  htmlFor,
  children,
}: {
  htmlFor?: string;
  children: ReactNode;
}) {
  return (
    <label
      htmlFor={htmlFor}
      className="mb-1 block text-xs font-semibold text-slate-700"
    >
      {children}
    </label>
  );
}

/** A row of pill buttons; clicking the selected pill clears the answer. */
function ChipGroup({
  name,
  label,
  options,
}: {
  name: string;
  label?: string;
  options: readonly Option[];
}) {
  const { control } = useFormContext();
  return (
    <Controller
      name={name}
      control={control}
      render={({ field: { value, onChange } }) => (
        <div role="radiogroup" aria-label={label ?? name}>
          {label && <FieldLabel>{label}</FieldLabel>}
          <div className="flex flex-wrap gap-1.5">
            {options.map((option) => {
              const selected = value === option.value;
              return (
                <button
                  key={option.value}
                  type="button"
                  role="radio"
                  aria-checked={selected}
                  onClick={() => onChange(selected ? "" : option.value)}
                  className={clsx(
                    "rounded-full border px-3 py-1 text-xs transition",
                    selected
                      ? "border-indigo-700 bg-indigo-700 font-semibold text-white"
                      : "border-slate-300 bg-white text-slate-700 hover:border-slate-400",
                  )}
                >
                  {option.label}
                </button>
              );
            })}
          </div>
        </div>
      )}
    />
  );
}

function SelectField({
  name,
  label,
  options,
}: {
  name: string;
  label: string;
  options: readonly Option[];
}) {
  const { register } = useFormContext();
  const id = `dental-${name}`;
  return (
    <div>
      <FieldLabel htmlFor={id}>{label}</FieldLabel>
      <select id={id} className={inputClass} {...register(name)}>
        <option value="">Select...</option>
        {options.map((option) => (
          <option key={option.value} value={option.value}>
            {option.label}
          </option>
        ))}
      </select>
    </div>
  );
}

function TextField({
  name,
  label,
  placeholder,
  type = "text",
}: {
  name: string;
  label?: string;
  placeholder?: string;
  type?: "text" | "date";
}) {
  const { register } = useFormContext();
  const id = `dental-${name}`;
  return (
    <div>
      {label && <FieldLabel htmlFor={id}>{label}</FieldLabel>}
      <input
        id={id}
        type={type}
        placeholder={placeholder}
        aria-label={label ? undefined : placeholder}
        className={inputClass}
        {...register(name)}
      />
    </div>
  );
}

function DiagnosisList() {
  const { control, register } = useFormContext<DentalFormValues>();
  const { fields, append, remove } = useFieldArray({
    control,
    name: "diagnoses",
  });
  return (
    <div className="space-y-2">
      {fields.map((field, index) => (
        <div key={field.id} className="flex gap-2">
          <input
            aria-label={`Diagnosis ${index + 1}`}
            placeholder="e.g. Dental caries"
            className={inputClass}
            {...register(`diagnoses.${index}.value`)}
          />
          <button
            type="button"
            onClick={() => remove(index)}
            className="rounded-md border border-slate-300 px-3 text-sm text-slate-600 hover:bg-slate-50"
          >
            Remove
          </button>
        </div>
      ))}
      <button
        type="button"
        onClick={() => append({ value: "" })}
        className="rounded-md border border-indigo-900 bg-indigo-50 px-3 py-1.5 text-sm text-indigo-900 hover:bg-indigo-100"
      >
        Add Diagnosis +
      </button>
    </div>
  );
}

function DentalFields() {
  return (
    <div className="space-y-4">
      <Card index={0}>
        <div className="grid grid-cols-1 gap-4 md:grid-cols-3">
          <ChipGroup
            name="previousDentalVisit"
            label="Previous dental visit"
            options={YES_NO_UNKNOWN}
          />
          <ChipGroup
            name="previousDentalTreatment"
            label="Previous dental treatment"
            options={YES_NO_UNKNOWN}
          />
          {HISTORY_YES_NO.map((q) => (
            <ChipGroup
              key={q.name}
              name={q.name}
              label={q.label}
              options={YES_NO}
            />
          ))}
          <ChipGroup
            name="brushingFrequency"
            label="Brushing frequency"
            options={BRUSHING_FREQUENCY}
          />
          <ChipGroup
            name="fluorideToothpaste"
            label="Fluoride toothpaste"
            options={YES_NO_UNKNOWN}
          />
          <ChipGroup
            name="sugarConsumption"
            label="Sugar consumption"
            options={SUGAR_CONSUMPTION}
          />
        </div>
        <div className="mt-4">
          <TextField
            name="otherDentalHistory"
            label="Other dental history (optional)"
            placeholder="e.g. braces, previous filling..."
          />
        </div>
      </Card>

      <Card index={1}>
        <TextField
          name="chiefComplaint"
          placeholder="What brings the patient to the dental station?"
        />
      </Card>

      <Card index={2}>
        <h3 className="mb-2 text-xs font-bold uppercase tracking-wide text-slate-500">
          A. Extraoral examination
        </h3>
        <div className="grid grid-cols-1 gap-4 md:grid-cols-3">
          {EXTRAORAL_EXAM.map((q) => (
            <SelectField key={q.name} {...q} />
          ))}
        </div>
        <h3 className="mb-2 mt-5 text-xs font-bold uppercase tracking-wide text-slate-500">
          B. Intraoral examination
        </h3>
        <div className="grid grid-cols-1 gap-4 md:grid-cols-3">
          {INTRAORAL_EXAM.map((q) => (
            <SelectField key={q.name} {...q} />
          ))}
        </div>
      </Card>

      <Card index={3}>
        <DiagnosisList />
      </Card>

      <Card index={4}>
        <div className="divide-y divide-slate-100">
          {TREATMENTS.map(({ key, label }) => (
            <div
              key={key}
              className="grid grid-cols-1 items-center gap-2 py-2 md:grid-cols-[5rem_10rem_1fr_1fr_1fr]"
            >
              <span className="text-sm font-semibold text-slate-800">
                {label}
              </span>
              <ChipGroup
                name={`${key}Done`}
                label={undefined}
                options={TREATMENT_STATUS}
              />
              <TextField
                name={`${key}Date`}
                type="date"
                placeholder={`${label} date`}
              />
              <TextField
                name={`${key}Operator`}
                placeholder={`${label} operator`}
              />
              <TextField
                name={`${key}ToothNumbers`}
                placeholder={`${label} tooth no.`}
              />
            </div>
          ))}
        </div>
      </Card>

      <Card index={5}>
        <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
          <ChipGroup
            name="referredFor"
            label="Referred for"
            options={REFERRED_FOR}
          />
          <ChipGroup
            name="referralPriority"
            label="Priority"
            options={REFERRAL_PRIORITY}
          />
          <TextField
            name="referralNotes"
            label="Referral notes"
            placeholder="e.g. Deep caries, needs restorative treatment"
          />
          <TextField
            name="recommendedFacility"
            label="Recommended facility"
            placeholder="e.g. Provincial Dental Clinic"
          />
        </div>
      </Card>

      <Card index={6}>
        <DentalRemarks />
      </Card>
    </div>
  );
}

function DentalRemarks() {
  const { register } = useFormContext();
  return (
    <textarea
      aria-label="Remarks"
      rows={3}
      placeholder="Additional notes..."
      className={inputClass}
      {...register("remarks")}
    />
  );
}

function SectionTabs() {
  return (
    <nav
      aria-label="Dental consultation sections"
      className="sticky top-0 z-10 -mx-1 mb-4 flex gap-1 overflow-x-auto border-b border-slate-200 bg-slate-50 px-1"
    >
      {SECTIONS.filter((s) => s.tab).map((section) => (
        <a
          key={section.id}
          href={`#${section.id}`}
          className="whitespace-nowrap border-b-2 border-transparent px-3 py-2 text-sm text-slate-600 hover:border-indigo-900 hover:text-indigo-900"
        >
          {section.tab}
        </a>
      ))}
    </nav>
  );
}

/**
 * The dental station's consultation for one visit. Loads any saved consultation
 * and saves the whole form back with a single PUT.
 */
export function DentalForm({ visitId }: { visitId: number }) {
  const methods = useForm<DentalFormValues>({
    defaultValues: toFormValues(null),
  });
  const { reset, handleSubmit } = methods;
  const queryClient = useQueryClient();

  // A visit without a dental consultation answers 404; that is just an empty form.
  const { data, isLoading } = useQuery({
    ...saBaiAPIEndpointsDentalGetByVisitGetEndpointQueryOptions(
      { path: { visitId } },
      apiConfig,
    ),
    enabled: !!visitId,
    retry: false,
  });

  const save = useMutation(
    saBaiAPIEndpointsDentalUpsertByVisitPutEndpointMutationOptions(apiConfig),
  );

  useEffect(() => {
    if (isLoading) return;
    reset(toFormValues(data));
  }, [data, isLoading, reset, visitId]);

  if (isLoading)
    return <LoadingSpinner message="Loading dental consultation" />;

  const onSubmit = (values: DentalFormValues) => {
    save.mutate(
      { path: { visitId }, body: toRequest(values) },
      {
        onSuccess: (saved) => {
          queryClient.setQueryData(
            saBaiAPIEndpointsDentalGetByVisitGetEndpointQueryKey({
              path: { visitId },
            }),
            saved,
          );
          reset(toFormValues(saved as Response));
          toast.success("Dental consultation saved.");
        },
        onError: (error) =>
          toast.error(
            getApiErrorMessage(error, "Failed to save dental consultation."),
          ),
      },
    );
  };

  return (
    <FormProvider {...methods}>
      <form onSubmit={handleSubmit(onSubmit)} aria-label="Dental consultation">
        <SectionTabs />
        <DentalFields />
        <div className="mt-6 flex items-center justify-between gap-4">
          <button
            type="button"
            onClick={() => reset(toFormValues(data))}
            className="rounded-md border border-indigo-900 px-4 py-2 text-sm text-indigo-900 hover:bg-indigo-50"
          >
            Reset Form
          </button>
          <div className="w-full md:w-56">
            <Button
              type="submit"
              title="Save Dental Consultation"
              colour="emerald"
              variant="filled"
              loading={save.isPending}
            />
          </div>
        </div>
      </form>
    </FormProvider>
  );
}
