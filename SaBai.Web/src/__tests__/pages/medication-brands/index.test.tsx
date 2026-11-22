import { cleanup, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import MedicationBrandsBasePage from "@/pages/medication-brands";
import {
  assertBreadcrumbs,
  assertLoadingSpinner,
  assertTableContents,
} from "@/__tests__/utils/helper-functions";
import { Toaster, toast } from "react-hot-toast";
import { renderWithQueryClient } from "@/__tests__/utils/renderWithQueryClient";

// The page and its child components use the generated SDK hooks. Override only
// the option factories they consume; the `...QueryKey` helpers are pure and
// come from the real module.
const mocks = vi.hoisted(() => ({
  brands: { data: undefined as unknown, loading: false },
  ingredients: { data: undefined as unknown, loading: false },
  create: vi.fn(),
  update: vi.fn(),
  remove: vi.fn(),
}));

vi.mock("@sabai/sdk/hooks", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@sabai/sdk/hooks")>();
  return {
    ...actual,
    saBaiAPIEndpointsMedicationBrandsListGetEndpointQueryOptions: () => ({
      queryKey:
        actual.saBaiAPIEndpointsMedicationBrandsListGetEndpointQueryKey(),
      queryFn: () =>
        mocks.brands.loading
          ? new Promise(() => {})
          : Promise.resolve(mocks.brands.data),
      initialData: mocks.brands.loading ? undefined : mocks.brands.data,
      staleTime: Infinity,
    }),
    saBaiAPIEndpointsMedicationIngredientsListGetEndpointQueryOptions: () => ({
      queryKey:
        actual.saBaiAPIEndpointsMedicationIngredientsListGetEndpointQueryKey(),
      queryFn: () =>
        mocks.ingredients.loading
          ? new Promise(() => {})
          : Promise.resolve(mocks.ingredients.data),
      initialData: mocks.ingredients.loading
        ? undefined
        : mocks.ingredients.data,
      staleTime: Infinity,
    }),
    saBaiAPIEndpointsMedicationBrandsCreatePostEndpointMutationOptions: () => ({
      mutationKey: ["medication-brands-create"],
      mutationFn: mocks.create,
    }),
    saBaiAPIEndpointsMedicationBrandsUpdatePatchEndpoint1MutationOptions:
      () => ({
        mutationKey: ["medication-brands-update"],
        mutationFn: mocks.update,
      }),
    saBaiAPIEndpointsMedicationBrandsDeleteDeleteEndpointMutationOptions:
      () => ({
        mutationKey: ["medication-brands-delete"],
        mutationFn: mocks.remove,
      }),
  };
});

const PARACETAMOL_ID = "11111111-1111-1111-1111-111111111111";
const IBUPROFEN_ID = "22222222-2222-2222-2222-222222222222";
const PANADOL_ID = "33333333-3333-3333-3333-333333333333";

const mockActiveIngredients = [
  {
    id: PARACETAMOL_ID,
    name: "Paracetamol",
    unitOfMeasurement: "mg",
    fallBelow: 3000,
    remarks: "test",
  },
  {
    id: IBUPROFEN_ID,
    name: "Ibuprofen",
    unitOfMeasurement: "tablets",
    fallBelow: 100,
    remarks: null,
  },
];

const mockBrands = [
  {
    id: PANADOL_ID,
    name: "Panadol",
    activeIngredientId: PARACETAMOL_ID,
    remarks: "test",
  },
];

describe("MedicationBrandsBasePage", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    toast.dismissAll();

    mocks.brands.data = mockBrands;
    mocks.brands.loading = false;
    mocks.ingredients.data = mockActiveIngredients;
    mocks.ingredients.loading = false;
    mocks.create.mockResolvedValue(undefined);
    mocks.update.mockResolvedValue(undefined);
    mocks.remove.mockResolvedValue(undefined);
  });

  afterEach(() => {
    cleanup();
    toast.dismissAll();
  });

  it("renders page title and breadcrumbs", () => {
    renderWithQueryClient(<MedicationBrandsBasePage />);

    expect(
      screen.getByRole("heading", { name: "Medication Brands" }),
    ).toBeInTheDocument();

    assertBreadcrumbs(["Home", "Medication Stock", "Medication Brands"]);
  });

  it("displays loading state", () => {
    mocks.brands.loading = true;
    mocks.brands.data = undefined;

    renderWithQueryClient(<MedicationBrandsBasePage />);

    assertLoadingSpinner("Loading brands...");
  });

  it("displays an empty-state message when there are no brands", () => {
    mocks.brands.data = [];

    renderWithQueryClient(<MedicationBrandsBasePage />);

    expect(
      screen.getByText(
        "No medication brands found. Seed the database or add a new record.",
      ),
    ).toBeInTheDocument();
  });

  it("displays brands and active ingredient details in the table", async () => {
    renderWithQueryClient(<MedicationBrandsBasePage />);

    assertTableContents(await screen.findByRole("table"), [
      [
        "Brand ID",
        "Brand Name",
        "Active Ingredient Name",
        "Unit of Measurement",
        "Remarks",
        "Actions",
      ],
      [PANADOL_ID, "Panadol", "Paracetamol", "mg", "test", "EditDelete"],
    ]);
  });

  it("changes EditableCell to edit state when Edit is clicked", async () => {
    const user = userEvent.setup();
    const { container } = renderWithQueryClient(<MedicationBrandsBasePage />);

    expect(screen.getByText("Panadol")).toBeInTheDocument();
    expect(screen.getByText("Paracetamol")).toBeInTheDocument();

    expect(
      container.querySelector('input[name="name"]'),
    ).not.toBeInTheDocument();
    expect(
      container.querySelector('textarea[name="remarks"]'),
    ).not.toBeInTheDocument();
    expect(
      container.querySelector(
        'button[name="activeIngredientId-dropdown-button"]',
      ),
    ).not.toBeInTheDocument();

    await user.click(screen.getByRole("button", { name: "Edit" }));

    expect(container.querySelector('input[name="name"]')).toHaveValue(
      "Panadol",
    );
    expect(container.querySelector('textarea[name="remarks"]')).toHaveValue(
      "test",
    );
    expect(
      container.querySelector(
        'button[name="activeIngredientId-dropdown-button"]',
      ),
    ).toHaveTextContent("Paracetamol");

    expect(screen.getByText("mg")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Save" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Cancel" })).toBeInTheDocument();
    expect(
      screen.queryByRole("button", { name: "Edit" }),
    ).not.toBeInTheDocument();
    expect(
      screen.queryByRole("button", { name: "Delete" }),
    ).not.toBeInTheDocument();
  });

  it("discards edits and returns to normal state when Cancel is clicked", async () => {
    const user = userEvent.setup();
    const { container } = renderWithQueryClient(<MedicationBrandsBasePage />);

    await user.click(screen.getByRole("button", { name: "Edit" }));

    const nameInput =
      container.querySelector<HTMLInputElement>('input[name="name"]')!;

    await user.clear(nameInput);
    await user.type(nameInput, "Unsaved Brand");

    await user.click(screen.getByRole("button", { name: "Cancel" }));

    expect(screen.getByText("Panadol")).toBeInTheDocument();
    expect(screen.queryByText("Unsaved Brand")).not.toBeInTheDocument();
    expect(
      container.querySelector('input[name="name"]'),
    ).not.toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Edit" })).toBeInTheDocument();
  });

  it("displays a toast rejecting edits when no field changed", async () => {
    const user = userEvent.setup();

    renderWithQueryClient(
      <>
        <MedicationBrandsBasePage />
        <Toaster />
      </>,
    );

    await user.click(screen.getByRole("button", { name: "Edit" }));
    await user.click(screen.getByRole("button", { name: "Save" }));

    expect(await screen.findByRole("status")).toHaveTextContent(
      "No form field changed!",
    );
    expect(mocks.update).not.toHaveBeenCalled();
  });

  it("displays a success toast after a valid edit", async () => {
    const user = userEvent.setup();

    const { container } = renderWithQueryClient(
      <>
        <MedicationBrandsBasePage />
        <Toaster />
      </>,
    );

    await user.click(screen.getByRole("button", { name: "Edit" }));

    const nameInput =
      container.querySelector<HTMLInputElement>('input[name="name"]')!;

    await user.clear(nameInput);
    await user.type(nameInput, "Updated Brand");
    await user.click(screen.getByRole("button", { name: "Save" }));

    expect(await screen.findByRole("status")).toHaveTextContent(
      "Successfully updated!",
    );
    expect(mocks.update.mock.calls[0][0]).toEqual({
      path: { id: PANADOL_ID },
      body: {
        name: "Updated Brand",
        activeIngredientId: PARACETAMOL_ID,
        remarks: "test",
      },
    });
  });

  it("sends an empty string for remarks when they are cleared", async () => {
    const user = userEvent.setup();

    const { container } = renderWithQueryClient(<MedicationBrandsBasePage />);

    await user.click(screen.getByRole("button", { name: "Edit" }));
    await user.clear(
      container.querySelector<HTMLTextAreaElement>('textarea[name="remarks"]')!,
    );
    await user.click(screen.getByRole("button", { name: "Save" }));

    expect(mocks.update.mock.calls[0][0]).toEqual({
      path: { id: PANADOL_ID },
      body: {
        name: "Panadol",
        activeIngredientId: PARACETAMOL_ID,
        remarks: "",
      },
    });
  });

  it("updates the ingredient and displays its unit", async () => {
    const user = userEvent.setup();

    const { container } = renderWithQueryClient(<MedicationBrandsBasePage />);

    await user.click(screen.getByRole("button", { name: "Edit" }));

    const dropdown = container.querySelector(
      'button[name="activeIngredientId-dropdown-button"]',
    )!;

    expect(dropdown).toHaveTextContent("Paracetamol");

    await user.click(dropdown);
    await user.click(screen.getByRole("button", { name: "Ibuprofen" }));

    expect(screen.getByText("tablets")).toBeInTheDocument();

    await user.click(screen.getByRole("button", { name: "Save" }));

    expect(mocks.update.mock.calls[0][0]).toEqual({
      path: { id: PANADOL_ID },
      body: {
        name: "Panadol",
        activeIngredientId: IBUPROFEN_ID,
        remarks: "test",
      },
    });
  });

  it("displays the brand details in the deletion confirmation modal", async () => {
    const user = userEvent.setup();

    renderWithQueryClient(<MedicationBrandsBasePage />);

    await user.click(screen.getByRole("button", { name: "Delete" }));

    const dialog = await screen.findByRole("dialog");

    expect(
      within(dialog).getByRole("heading", { name: "Confirm Deletion?" }),
    ).toBeInTheDocument();
    expect(within(dialog).getByText("Panadol")).toBeInTheDocument();
    expect(within(dialog).getByText("Paracetamol")).toBeInTheDocument();
    expect(within(dialog).getByText("mg")).toBeInTheDocument();
    expect(within(dialog).getByText("test")).toBeInTheDocument();
  });

  it("closes the deletion modal using Cancel or Close", async () => {
    const user = userEvent.setup();

    renderWithQueryClient(<MedicationBrandsBasePage />);

    await user.click(screen.getByRole("button", { name: "Delete" }));

    let dialog = await screen.findByRole("dialog");

    await user.click(within(dialog).getByRole("button", { name: "Cancel" }));
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();

    await user.click(screen.getByRole("button", { name: "Delete" }));

    dialog = await screen.findByRole("dialog");

    await user.click(within(dialog).getByRole("button", { name: "Close" }));
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
    expect(mocks.remove).not.toHaveBeenCalled();
  });

  it("shows a dependency error toast and closes the deletion modal", async () => {
    const user = userEvent.setup();

    mocks.remove.mockRejectedValue(new Error("in use"));

    renderWithQueryClient(
      <>
        <MedicationBrandsBasePage />
        <Toaster />
      </>,
    );

    await user.click(screen.getByRole("button", { name: "Delete" }));

    const dialog = await screen.findByRole("dialog");

    await user.click(within(dialog).getByRole("button", { name: "Confirm" }));

    expect(mocks.remove.mock.calls[0][0]).toEqual({ path: { id: PANADOL_ID } });
    expect(await screen.findByRole("status")).toHaveTextContent("in use");
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
  });

  it("displays a success toast after deletion", async () => {
    const user = userEvent.setup();

    renderWithQueryClient(
      <>
        <MedicationBrandsBasePage />
        <Toaster />
      </>,
    );

    await user.click(screen.getByRole("button", { name: "Delete" }));

    const dialog = await screen.findByRole("dialog");

    await user.click(within(dialog).getByRole("button", { name: "Confirm" }));

    expect(mocks.remove.mock.calls[0][0]).toEqual({ path: { id: PANADOL_ID } });
    expect(await screen.findByRole("status")).toHaveTextContent(
      "Successfully deleted.",
    );
  });

  it("opens the add brand modal", async () => {
    const user = userEvent.setup();

    renderWithQueryClient(<MedicationBrandsBasePage />);

    await user.click(screen.getByRole("button", { name: "Add Brand" }));

    const dialog = await screen.findByRole("dialog");

    expect(
      within(dialog).getByRole("heading", { name: "Add New Brand" }),
    ).toBeInTheDocument();
    expect(dialog.querySelector('input[name="name"]')).toBeInTheDocument();
    expect(dialog.querySelector('input[name="remarks"]')).toBeInTheDocument();
    expect(
      dialog.querySelector('button[name="activeIngredientId-dropdown-button"]'),
    ).toBeInTheDocument();
  });

  it("requires an active ingredient before creating a brand", async () => {
    const user = userEvent.setup();

    renderWithQueryClient(<MedicationBrandsBasePage />);

    await user.click(screen.getByRole("button", { name: "Add Brand" }));

    const dialog = await screen.findByRole("dialog");

    await user.type(
      dialog.querySelector<HTMLInputElement>('input[name="name"]')!,
      "New Brand",
    );
    await user.click(within(dialog).getByRole("button", { name: "Save" }));

    expect(
      await within(dialog).findByText("Missing Active Ingredient!"),
    ).toBeInTheDocument();
    expect(mocks.create).not.toHaveBeenCalled();
  });

  it("closes the add brand modal using Cancel or Close", async () => {
    const user = userEvent.setup();

    renderWithQueryClient(<MedicationBrandsBasePage />);

    await user.click(screen.getByRole("button", { name: "Add Brand" }));

    let dialog = await screen.findByRole("dialog");

    await user.click(within(dialog).getByRole("button", { name: "Cancel" }));
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();

    await user.click(screen.getByRole("button", { name: "Add Brand" }));

    dialog = await screen.findByRole("dialog");

    await user.click(within(dialog).getByRole("button", { name: "Close" }));
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
    expect(mocks.create).not.toHaveBeenCalled();
  });

  async function fillAndSaveNewBrand(
    user: ReturnType<typeof userEvent.setup>,
    remarks?: string,
  ) {
    await user.click(screen.getByRole("button", { name: "Add Brand" }));

    const dialog = await screen.findByRole("dialog");

    await user.type(
      dialog.querySelector<HTMLInputElement>('input[name="name"]')!,
      "New Brand",
    );
    if (remarks) {
      await user.type(
        dialog.querySelector<HTMLInputElement>('input[name="remarks"]')!,
        remarks,
      );
    }

    await user.click(
      dialog.querySelector(
        'button[name="activeIngredientId-dropdown-button"]',
      )!,
    );
    await user.click(
      within(dialog).getByRole("button", { name: "Paracetamol" }),
    );
    await user.click(within(dialog).getByRole("button", { name: "Save" }));
  }

  it("creates a brand and closes the modal", async () => {
    const user = userEvent.setup();

    renderWithQueryClient(
      <>
        <MedicationBrandsBasePage />
        <Toaster />
      </>,
    );

    await fillAndSaveNewBrand(user, "New remarks");

    expect(await screen.findByRole("status")).toHaveTextContent(
      "Successfully created!",
    );
    expect(mocks.create.mock.calls[0][0]).toEqual({
      body: {
        name: "New Brand",
        activeIngredientId: PARACETAMOL_ID,
        remarks: "New remarks",
      },
    });
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
  });

  it("sends an empty string for remarks when creating a brand without remarks", async () => {
    const user = userEvent.setup();

    renderWithQueryClient(<MedicationBrandsBasePage />);

    await fillAndSaveNewBrand(user);

    await vi.waitFor(() => expect(mocks.create).toHaveBeenCalled());
    expect(mocks.create.mock.calls[0][0]).toEqual({
      body: {
        name: "New Brand",
        activeIngredientId: PARACETAMOL_ID,
        remarks: "",
      },
    });
  });

  it("shows an error toast inside the add brand modal when creation fails", async () => {
    const user = userEvent.setup();
    mocks.create.mockRejectedValue(new Error("Brand already exists."));

    renderWithQueryClient(<MedicationBrandsBasePage />);

    await fillAndSaveNewBrand(user);

    const dialog = screen.getByRole("dialog");
    expect(await within(dialog).findByRole("status")).toHaveTextContent(
      "Brand already exists.",
    );
  });
});
