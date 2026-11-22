import { ConsultForm } from "@/components/consults/ConsultForm";
import { Toaster, toast } from "react-hot-toast";
import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { renderWithQueryClient } from "@/__tests__/utils/renderWithQueryClient";

// The component now uses the generated SDK mutation factory. Override only the
// factory it consumes and keep the pure query-key helpers from the real module.
const mocks = vi.hoisted(() => ({
  createConsult: vi.fn(),
  createOrder: vi.fn(),
  availability: [
    {
      ingredientId: "ing-para",
      ingredientName: "Paracetamol",
      unitOfMeasurement: "tablet",
      availableQuantity: 20,
    },
    {
      ingredientId: "ing-salb",
      ingredientName: "Salbutamol",
      unitOfMeasurement: "puff",
      availableQuantity: 0,
    },
  ],
}));

vi.mock("@sabai/sdk/hooks", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@sabai/sdk/hooks")>();
  return {
    ...actual,
    saBaiAPIEndpointsConsultsCreatePostEndpointMutationOptions: () => ({
      mutationKey: ["consults-create"],
      mutationFn: (variables: unknown) => mocks.createConsult(variables),
    }),
    saBaiAPIEndpointsOrdersCreatePostEndpointMutationOptions: () => ({
      mutationKey: ["orders-create"],
      mutationFn: (variables: unknown) => mocks.createOrder(variables),
    }),
    saBaiAPIEndpointsMedicationStockByIngredientGetEndpointQueryOptions:
      () => ({
        queryKey: ["by-ingredient"],
        queryFn: () => Promise.resolve(mocks.availability),
      }),
  };
});

const VISIT_ID = "01930000-0000-7000-8000-000000000010";

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

    mocks.createConsult.mockResolvedValue({ id: "consult-1" });
    mocks.createOrder.mockResolvedValue({ id: "order-1" });
  });

  /** Opens the order modal and stages an order through it. */
  async function stageOrder(
    user: ReturnType<typeof userEvent.setup>,
    medicine: RegExp,
    quantity: string,
    dosage: string,
  ) {
    await user.click(screen.getByRole("button", { name: "Add Order" }));
    await user.click(
      await screen.findByRole("button", { name: "Select an option" }),
    );
    await user.click(screen.getByRole("button", { name: medicine }));
    await user.type(screen.getByLabelText(/Quantity to Order/), quantity);
    await user.type(screen.getByLabelText(/Dosage Instructions/), dosage);
    await user.click(screen.getByRole("button", { name: "Save Order" }));
  }

  test("lists every ingredient with what is available and disables the out-of-stock ones", async () => {
    const user = userEvent.setup();
    renderForm();

    await user.click(screen.getByRole("button", { name: "Add Order" }));
    await user.click(
      await screen.findByRole("button", { name: "Select an option" }),
    );

    expect(
      screen.getByRole("button", { name: "Paracetamol (tablet) (Qty: 20)" }),
    ).toBeEnabled();
    expect(
      screen.getByRole("button", { name: "Salbutamol (puff) — out of stock" }),
    ).toBeDisabled();
  });

  test("refuses a quantity above what is available, counting staged orders", async () => {
    const user = userEvent.setup();
    renderForm();

    await stageOrder(user, /^Paracetamol/, "15", "1 tablet");
    expect(screen.getByText("Quantity: 15 tablet")).toBeInTheDocument();

    await stageOrder(
      user,
      /^Paracetamol \(tablet\) \(Qty: 5\)$/,
      "6",
      "1 tablet",
    );
    expect(
      await screen.findByText("Only 5 tablet available."),
    ).toBeInTheDocument();
    expect(screen.queryByText("Quantity: 6 tablet")).not.toBeInTheDocument();
  });

  test("places staged orders against the saved consult", async () => {
    const user = userEvent.setup();
    renderForm();

    await stageOrder(user, /^Paracetamol/, "10", "2 tablets daily");
    await fillValidForm(user);
    await user.click(screen.getByRole("button", { name: "Save Consult" }));

    await waitFor(() => expect(mocks.createOrder).toHaveBeenCalledTimes(1));
    expect(mocks.createOrder).toHaveBeenCalledWith({
      body: {
        consultId: "consult-1",
        medicationActiveIngredientId: "ing-para",
        quantity: 10,
        dosageInstructions: "2 tablets daily",
      },
    });
    await waitFor(() =>
      expect(screen.getByRole("status").textContent).toBe(
        "Consult has been saved successfully!",
      ),
    );
  });

  test("keeps the consult and names an order the server could not fill", async () => {
    const user = userEvent.setup();
    mocks.createOrder.mockRejectedValue(
      Object.assign(new Error("Conflict"), {
        status: 409,
        data: { detail: "Insufficient stock: 4 of 10 available." },
      }),
    );
    renderForm();

    await stageOrder(user, /^Paracetamol/, "10", "2 tablets daily");
    await fillValidForm(user);
    await user.click(screen.getByRole("button", { name: "Save Consult" }));

    await waitFor(() =>
      expect(screen.getByRole("status").textContent).toBe(
        "Consult saved, but 1 order(s) could not be filled: Paracetamol (Insufficient stock: 4 of 10 available.). Please re-order from the pharmacy.",
      ),
    );
    expect(mocks.createConsult).toHaveBeenCalledTimes(1);
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
