import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import {
  DentalForm,
  toFormValues,
  toRequest,
} from "@/components/dental/DentalForm";
import { renderWithQueryClient } from "@/__tests__/utils/renderWithQueryClient";

const mocks = vi.hoisted(() => ({
  saved: undefined as unknown,
  put: vi.fn(),
}));

vi.mock("@sabai/sdk/hooks", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@sabai/sdk/hooks")>();
  return {
    ...actual,
    saBaiAPIEndpointsDentalGetByVisitGetEndpointQueryOptions: () => ({
      queryKey: ["dental"],
      queryFn: () =>
        mocks.saved
          ? Promise.resolve(mocks.saved)
          : Promise.reject(new Error("not found")),
    }),
    saBaiAPIEndpointsDentalUpsertByVisitPutEndpointMutationOptions: () => ({
      mutationFn: (vars: unknown) => {
        mocks.put(vars);
        return Promise.resolve({ id: 1, visitId: 7, diagnoses: [] });
      },
    }),
  };
});

describe("dental form mapping", () => {
  it("round-trips answers between the API and the form", () => {
    const values = toFormValues({
      id: 1,
      visitId: 7,
      toothache: false,
      previousDentalVisit: "Unknown",
      caries: "NonCavitated",
      diagnoses: ["Caries"],
      fluorideDone: true,
      fluorideDate: "2026-11-02",
      updatedAt: "2026-11-02T00:00:00Z",
    });

    expect(values.toothache).toBe("no");
    expect(values.caries).toBe("NonCavitated");
    expect(values.fluorideDone).toBe("done");
    expect(values.sealantDone).toBe("");

    const body = toRequest({
      ...values,
      chiefComplaint: "  Pain ",
      diagnoses: [{ value: "Caries" }, { value: "  " }],
    });
    expect(body.toothache).toBe(false);
    expect(body.bleedingGums).toBeNull();
    expect(body.previousDentalVisit).toBe("Unknown");
    expect(body.chiefComplaint).toBe("Pain");
    expect(body.fluorideDone).toBe(true);
    expect(body.fluorideDate).toBe("2026-11-02");
    expect(body.plaque).toBeNull();
    expect(body.diagnoses).toEqual(["Caries"]);
  });
});

describe("DentalForm", () => {
  beforeEach(() => {
    mocks.saved = undefined;
    mocks.put.mockClear();
  });

  it("renders every clinical examination question with its options", async () => {
    renderWithQueryClient(<DentalForm visitId={7} />);

    const caries = await screen.findByLabelText("Caries");
    expect(
      Array.from((caries as HTMLSelectElement).options).map((o) => o.text),
    ).toEqual(["Select...", "Absent", "Non-cavitated", "Cavitated"]);
    expect(
      Array.from(
        (screen.getByLabelText("Lymphadenopathy") as HTMLSelectElement).options,
      ).map((o) => o.text),
    ).toEqual(["Select...", "Absent", "Present", "Not assessed"]);
    for (const label of [
      "Facial symmetry",
      "Facial swelling",
      "Mouth opening",
      "Oral hygiene",
      "Plaque",
      "Calculus",
      "Gingival condition",
      "Missing teeth",
      "Fractured teeth",
      "Mucosal lesions",
      "Malocclusion",
    ]) {
      expect(screen.getByLabelText(label)).toBeInTheDocument();
    }
  });

  it("saves the selected answers for the visit", async () => {
    const user = userEvent.setup();
    renderWithQueryClient(<DentalForm visitId={7} />);

    const toothache = await screen.findByRole("radiogroup", {
      name: "Toothache",
    });
    await user.click(
      Array.from(toothache.querySelectorAll("button")).find(
        (b) => b.textContent === "Yes",
      )!,
    );
    await user.selectOptions(screen.getByLabelText("Plaque"), "Heavy");
    await user.click(screen.getByRole("button", { name: "Add Diagnosis +" }));
    await user.type(screen.getByLabelText("Diagnosis 1"), "Gingivitis");
    await user.click(
      screen.getByRole("button", { name: /Save Dental Consultation/ }),
    );

    await waitFor(() => expect(mocks.put).toHaveBeenCalled());
    const { path, body } = mocks.put.mock.calls[0][0];
    expect(path).toEqual({ visitId: 7 });
    expect(body.toothache).toBe(true);
    expect(body.plaque).toBe("Heavy");
    expect(body.diagnoses).toEqual(["Gingivitis"]);
  });
});
