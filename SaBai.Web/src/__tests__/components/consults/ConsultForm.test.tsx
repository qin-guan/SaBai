import { ConsultForm } from "@/components/consults/ConsultForm";
import { Toaster, toast } from "react-hot-toast";
import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { renderWithQueryClient } from "@/__tests__/utils/renderWithQueryClient";

// The component now uses the generated SDK mutation factory. Override only the
// factory it consumes and keep the pure query-key helpers from the real module.
const mocks = vi.hoisted(() => ({
  createConsult: vi.fn(),
}));

vi.mock("@sabai/sdk/hooks", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@sabai/sdk/hooks")>();
  return {
    ...actual,
    saBaiAPIEndpointsConsultsCreatePostEndpointMutationOptions: () => ({
      mutationKey: ["consults-create"],
      mutationFn: mocks.createConsult,
    }),
  };
});

const VISIT_ID = 10;

function renderForm() {
  return renderWithQueryClient(
    <>
      <Toaster />
      <ConsultForm visitId={VISIT_ID} />
    </>,
  );
}

/** Fills the two required text fields and the single diagnosis with valid values. */
async function fillValidForm(user: ReturnType<typeof userEvent.setup>) {
  await user.type(screen.getByLabelText(/Past Medical History/), "No history");
  await user.type(screen.getByLabelText(/Consultation/), "Patient is well");
  await user.click(screen.getByRole("button", { name: "Choose a category" }));
  await user.click(screen.getByRole("button", { name: "Cardiovascular" }));
  await user.type(screen.getByLabelText(/Details/), "Hypertension");
}

describe("ConsultForm", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    toast.remove();

    mocks.createConsult.mockResolvedValue(undefined);
  });

  test("renders all fields and the save button", () => {
    renderForm();

    expect(screen.getByLabelText(/Past Medical History/)).toBeInTheDocument();
    expect(screen.getByLabelText(/Consultation/)).toBeInTheDocument();
    expect(screen.getByText("Diagnosis 1")).toBeInTheDocument();
    expect(screen.getByLabelText(/Plan/)).toBeInTheDocument();
    expect(screen.getByLabelText(/Remarks/)).toBeInTheDocument();
    expect(
      screen.getByRole("button", { name: "Save Consult" }),
    ).toBeInTheDocument();
  });

  test("blocks submit and shows an error toast when required fields are empty", async () => {
    const user = userEvent.setup();
    renderForm();

    await user.click(screen.getByRole("button", { name: "Save Consult" }));

    await waitFor(() => {
      const status = screen.getByRole("status");
      expect(status.textContent).toBe(
        "Please fill in all required fields before saving.",
      );
    });
    expect(mocks.createConsult).not.toHaveBeenCalled();
  });

  test("submits the mapped payload when the form fields are valid", async () => {
    const user = userEvent.setup();
    renderForm();

    await fillValidForm(user);
    await user.click(screen.getByRole("button", { name: "Save Consult" }));

    await waitFor(() => expect(mocks.createConsult).toHaveBeenCalledTimes(1));

    // react-query invokes the mutation function with the variables object.
    expect(mocks.createConsult).toHaveBeenCalledWith({
      body: expect.objectContaining({
        visitId: VISIT_ID,
        pastMedicalHistory: "No history",
        consultation: "Patient is well",
        // blank optional fields collapse to undefined
        treatmentPlan: undefined,
        remarks: undefined,
        diagnoses: [{ details: "Hypertension", category: "Cardiovascular" }],
      }),
    });
  });

  test("does not allow submit when diagnosis fields are empty", async () => {
    const user = userEvent.setup();
    renderForm();

    await user.type(
      screen.getByLabelText(/Past Medical History/),
      "No history",
    );
    await user.type(screen.getByLabelText(/Consultation/), "Patient is well");

    await user.click(screen.getByRole("button", { name: "Save Consult" }));

    await waitFor(() => {
      const status = screen.getByRole("status");
      expect(status.textContent).toBe(
        "Please fill in all required fields before saving.",
      );
    });
    expect(mocks.createConsult).not.toHaveBeenCalled();
  });

  test("adds and removes diagnosis fields", async () => {
    const user = userEvent.setup();
    renderForm();

    // only one diagnosis: Remove button is hidden
    expect(
      screen.queryByRole("button", { name: "Remove" }),
    ).not.toBeInTheDocument();

    await user.click(screen.getByRole("button", { name: "Add Diagnosis" }));
    expect(screen.getByText("Diagnosis 2")).toBeInTheDocument();

    // with two, both Remove buttons are visible; removing one hides the button again
    const removeButtons = screen.getAllByRole("button", { name: "Remove" });
    expect(removeButtons).toHaveLength(2);
    await user.click(removeButtons[0]);

    expect(screen.queryByText("Diagnosis 2")).not.toBeInTheDocument();
    expect(
      screen.queryByRole("button", { name: "Remove" }),
    ).not.toBeInTheDocument();
  });

  test("shows the save button in a loading state while pending", async () => {
    const user = userEvent.setup();
    // A never-resolving mutation keeps react-query's `isPending` true.
    mocks.createConsult.mockImplementation(() => new Promise(() => {}));

    renderForm();

    await fillValidForm(user);
    await user.click(screen.getByRole("button", { name: "Save Consult" }));

    await waitFor(() => {
      expect(
        screen.getByRole("button", { name: "Save Consult" }),
      ).toBeDisabled();
    });
  });
});
