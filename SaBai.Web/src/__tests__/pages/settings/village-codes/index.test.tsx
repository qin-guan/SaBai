import { screen, waitFor, cleanup } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import VillageCodesPage from "@/pages/settings/village-codes";
import { toast, Toaster } from "react-hot-toast";
import { assertLoadingSpinner } from "@/__tests__/utils/helper-functions";
import { renderWithQueryClient } from "@/__tests__/utils/renderWithQueryClient";

// The page and its modals now use the generated SDK hooks. The list query
// factory is wrapped in a spy so tests can assert the `includeHidden` argument;
// the mutation factories are overridden with plain react-query option objects.
const mocks = vi.hoisted(() => ({
  data: undefined as unknown,
  listLoading: false,
  listQueryOptions: vi.fn(),
  create: vi.fn(),
  update: vi.fn(),
  remove: vi.fn(),
}));

vi.mock("@sabai/sdk/hooks", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@sabai/sdk/hooks")>();
  return {
    ...actual,
    saBaiAPIEndpointsVillageCodesListGetEndpointQueryOptions: (
      args: unknown,
      config: unknown,
    ) => mocks.listQueryOptions(args, config),
    saBaiAPIEndpointsVillageCodesCreatePostEndpointMutationOptions: () => ({
      mutationKey: ["village-codes-create"],
      mutationFn: mocks.create,
    }),
    saBaiAPIEndpointsVillageCodesUpdatePatchEndpointMutationOptions: () => ({
      mutationKey: ["village-codes-update"],
      mutationFn: mocks.update,
    }),
    saBaiAPIEndpointsVillageCodesDeleteDeleteEndpointMutationOptions: () => ({
      mutationKey: ["village-codes-delete"],
      mutationFn: mocks.remove,
    }),
  };
});

const MOCK_VILLAGE_CODES = {
  PC: {
    id: 1,
    code: "PC",
    name: "PC Village",
    colorHex: "#FF5733",
    isVisible: true,
  },
  CA: {
    id: 2,
    code: "CA",
    name: "CA Village",
    colorHex: "#3498DB",
    isVisible: true,
  },
  TK: {
    id: 3,
    code: "TK",
    name: "TK Village",
    colorHex: "#2ECC71",
    isVisible: false,
  },
  SC: {
    code: "SC",
    name: "SC Village",
  },
} as const;

const FORM_PLACEHOLDERS = {
  CODE: "e.g. V001",
  NAME: "Central Village",
} as const;

const mockVillageCodes = [
  MOCK_VILLAGE_CODES.PC,
  MOCK_VILLAGE_CODES.CA,
  MOCK_VILLAGE_CODES.TK,
];

describe("VillageCodesPage", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    toast.remove(); // remove actually removes everything immediately.
    //dismiss might cause conflicts if multiple testcases are using toasts.
    window.localStorage.clear();

    mocks.data = undefined;
    mocks.listLoading = false;

    mocks.create.mockResolvedValue(undefined);
    mocks.update.mockResolvedValue(undefined);
    mocks.remove.mockResolvedValue(undefined);

    // Mirrors the API: the response only includes hidden codes when requested.
    mocks.listQueryOptions.mockImplementation(
      (args?: { query?: { includeHidden?: boolean } }) => {
        const includeHidden = args?.query?.includeHidden ?? false;
        const codes =
          mocks.data !== undefined
            ? (mocks.data as typeof mockVillageCodes)
            : includeHidden
              ? mockVillageCodes
              : mockVillageCodes.filter((vc) => vc.isVisible);

        return {
          queryKey: ["village-codes", { includeHidden }],
          queryFn: () =>
            mocks.listLoading
              ? new Promise(() => {})
              : Promise.resolve({ villageCodes: codes }),
          initialData: mocks.listLoading
            ? undefined
            : { villageCodes: codes },
          staleTime: Infinity,
        };
      },
    );
  });

  afterEach(() => {
    cleanup();
  });

  it("renders page title and breadcrumbs", () => {
    renderWithQueryClient(<VillageCodesPage />);

    expect(
      screen.getByRole("heading", { name: "Village Codes" }),
    ).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Home" })).toBeInTheDocument();
    expect(screen.getByText("Settings")).toBeInTheDocument();
  });

  it("displays loading state", () => {
    mocks.listLoading = true;

    renderWithQueryClient(<VillageCodesPage />);

    assertLoadingSpinner("Loading Village Codes...");
  });

  it("displays village codes in table", () => {
    mocks.data = mockVillageCodes;

    renderWithQueryClient(<VillageCodesPage />);

    expect(screen.getByText(MOCK_VILLAGE_CODES.PC.code)).toBeInTheDocument();
    expect(screen.getByText(MOCK_VILLAGE_CODES.PC.name)).toBeInTheDocument();
    expect(screen.getByText(MOCK_VILLAGE_CODES.CA.code)).toBeInTheDocument();
    expect(screen.getByText(MOCK_VILLAGE_CODES.CA.name)).toBeInTheDocument();
    expect(screen.getByText(MOCK_VILLAGE_CODES.TK.code)).toBeInTheDocument();
    expect(screen.getByText(MOCK_VILLAGE_CODES.TK.name)).toBeInTheDocument();
  });

  it("opens new village code form when button is clicked", async () => {
    const user = userEvent.setup();

    renderWithQueryClient(<VillageCodesPage />);

    await user.click(screen.getByRole("button", { name: "New Village Code" }));

    await waitFor(() => {
      expect(
        screen.getByRole("heading", { name: "New Village Code" }),
      ).toBeInTheDocument();
    });

    expect(
      screen.getByPlaceholderText(FORM_PLACEHOLDERS.CODE),
    ).toBeInTheDocument();
    expect(
      screen.getByPlaceholderText(FORM_PLACEHOLDERS.NAME),
    ).toBeInTheDocument();
  });

  it("opens edit form when edit button is clicked", async () => {
    const user = userEvent.setup();

    renderWithQueryClient(<VillageCodesPage />);

    await user.click(screen.getAllByText("Edit")[0]);

    await waitFor(() => {
      expect(screen.getByText("Edit Village Code")).toBeInTheDocument();
    });
    expect(
      screen.getByDisplayValue(MOCK_VILLAGE_CODES.PC.name),
    ).toBeInTheDocument();
  });

  it("shows a success toast when a village code has been successfully edited", async () => {
    const user = userEvent.setup();

    renderWithQueryClient(
      <>
        <VillageCodesPage />
        <Toaster toastOptions={{ duration: 100 }} />
      </>,
    );

    await user.click(screen.getAllByRole("button", { name: "Edit" })[0]);

    await waitFor(() => {
      expect(screen.getByText("Edit Village Code")).toBeInTheDocument();
    });

    const codeInput = document.querySelector('input[name="code"]');
    expect(codeInput).toBeInTheDocument();
    await user.type(codeInput!, "1111");

    expect(screen.queryByRole("status")).not.toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: "Save" }));

    const toastEl = await screen.findByRole("status");

    expect(toastEl).toBeInTheDocument();
    expect(toastEl).toHaveTextContent("Village code updated!");
    expect(mocks.update).toHaveBeenCalledWith({
      path: { id: MOCK_VILLAGE_CODES.PC.id },
      body: {
        code: "PC1111",
        name: MOCK_VILLAGE_CODES.PC.name,
        colorHex: MOCK_VILLAGE_CODES.PC.colorHex,
        isVisible: true,
      },
    });
  });

  it("closes form when cancel button is clicked", async () => {
    const user = userEvent.setup();

    renderWithQueryClient(<VillageCodesPage />);

    await user.click(screen.getByRole("button", { name: "New Village Code" }));
    await waitFor(
      () => {
        expect(screen.getByRole("dialog")).toBeInTheDocument();
      },
      { timeout: 3000 },
    );

    await user.click(screen.getByRole("button", { name: "Cancel" }));
    await waitFor(() => {
      expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
    });
  });

  it("submits new village code form with correct data", async () => {
    const user = userEvent.setup();

    renderWithQueryClient(<VillageCodesPage />);

    await user.click(screen.getByRole("button", { name: "New Village Code" }));

    await waitFor(() => {
      expect(
        screen.getByPlaceholderText(FORM_PLACEHOLDERS.CODE),
      ).toBeInTheDocument();
    });

    await user.type(
      screen.getByPlaceholderText(FORM_PLACEHOLDERS.CODE),
      MOCK_VILLAGE_CODES.SC.code,
    );
    await user.type(
      screen.getByPlaceholderText(FORM_PLACEHOLDERS.NAME),
      MOCK_VILLAGE_CODES.SC.name,
    );

    await user.click(screen.getByRole("button", { name: "Save" }));

    expect(mocks.create).toHaveBeenCalledWith({
      body: {
        code: MOCK_VILLAGE_CODES.SC.code,
        name: MOCK_VILLAGE_CODES.SC.name,
        colorHex: "#3b82f6", // This is the default color that the form uses
        isVisible: true,
      },
    });
  });

  it("shows a success toast when a new village code has been successfully created", async () => {
    const user = userEvent.setup();

    renderWithQueryClient(
      <>
        <VillageCodesPage />
        <Toaster toastOptions={{ duration: 100 }} />
      </>,
    );

    await user.click(screen.getByRole("button", { name: "New Village Code" }));

    await waitFor(() => {
      expect(
        screen.getByPlaceholderText(FORM_PLACEHOLDERS.CODE),
      ).toBeInTheDocument();
    });

    await user.type(
      screen.getByPlaceholderText(FORM_PLACEHOLDERS.CODE),
      MOCK_VILLAGE_CODES.SC.code,
    );
    await user.type(
      screen.getByPlaceholderText(FORM_PLACEHOLDERS.NAME),
      MOCK_VILLAGE_CODES.SC.name,
    );

    await user.click(screen.getByRole("button", { name: "Save" }));

    const toastEl = await screen.findByRole("status");

    expect(toastEl).toBeInTheDocument();
    expect(toastEl.textContent).toBe("Village code created!");
  });

  it("toggles show hidden checkbox", async () => {
    const user = userEvent.setup();

    renderWithQueryClient(<VillageCodesPage />);

    const showHiddenCheckbox = screen.getByLabelText("Show Hidden");

    expect(mocks.listQueryOptions).toHaveBeenCalledWith(
      { query: { includeHidden: false } },
      expect.anything(),
    );

    await user.click(showHiddenCheckbox);

    await waitFor(() => {
      expect(mocks.listQueryOptions).toHaveBeenCalledWith(
        { query: { includeHidden: true } },
        expect.anything(),
      );
    });
  });

  it("calls delete mutation when delete button is clicked and confirmed", async () => {
    const user = userEvent.setup();

    renderWithQueryClient(<VillageCodesPage />);

    await user.click(screen.getAllByText("Delete")[0]);

    const confirmButton = await screen.findByRole("button", {
      name: "Confirm",
    });

    await user.click(confirmButton);

    expect(mocks.remove).toHaveBeenCalledWith({
      path: { id: MOCK_VILLAGE_CODES.PC.id },
    });
  });

  it("shows a success toast when a village code has been successfully deleted", async () => {
    const user = userEvent.setup();

    renderWithQueryClient(
      <>
        <VillageCodesPage />
        <Toaster />
      </>,
    );

    const deleteButtons = screen.getAllByRole("button", { name: "Delete" });

    await user.click(deleteButtons[0]);

    const confirmButton = await screen.findByRole("button", {
      name: "Confirm",
    });

    await user.click(confirmButton);

    const toastEl = await screen.findByRole("status");
    expect(toastEl).toBeInTheDocument();
    expect(toastEl).toHaveTextContent("Village code deleted!");
  });

  it("does not delete when confirmation is cancelled", async () => {
    const user = userEvent.setup();

    renderWithQueryClient(<VillageCodesPage />);

    await user.click(screen.getAllByRole("button", { name: "Delete" })[0]);

    const cancelButton = await screen.findByRole("button", { name: "Cancel" });

    await user.click(cancelButton);

    expect(mocks.remove).not.toHaveBeenCalled();
  });

  it("shows an error message if deletion failed", async () => {
    const user = userEvent.setup();

    // An SDK error without a ProblemDetails body (no `message`) makes the
    // component fall back to its generic deletion message.
    mocks.remove.mockRejectedValue({ status: 500 });

    renderWithQueryClient(
      <>
        <VillageCodesPage />
        <Toaster />
      </>,
    );

    const deleteButtons = screen.getAllByRole("button", { name: "Delete" });

    await user.click(deleteButtons[0]);

    const confirmButton = await screen.findByRole("button", {
      name: "Confirm",
    });

    await user.click(confirmButton);

    const toastEl = await screen.findByRole("status");
    expect(toastEl).toBeInTheDocument();
    expect(toastEl).toHaveTextContent(
      "Unable to delete village code. Check that there are no visits with this village code before deleting.",
    );
  });

  it("shows only visible village codes by default (hidden checkbox unchecked)", () => {
    renderWithQueryClient(<VillageCodesPage />);

    // Should show visible village codes (PC and CA)
    expect(screen.getByText(MOCK_VILLAGE_CODES.PC.code)).toBeInTheDocument();
    expect(screen.getByText(MOCK_VILLAGE_CODES.PC.name)).toBeInTheDocument();
    expect(screen.getByText(MOCK_VILLAGE_CODES.CA.code)).toBeInTheDocument();
    expect(screen.getByText(MOCK_VILLAGE_CODES.CA.name)).toBeInTheDocument();

    // Should NOT show hidden village code (TK)
    expect(
      screen.queryByText(MOCK_VILLAGE_CODES.TK.code),
    ).not.toBeInTheDocument();
    expect(
      screen.queryByText(MOCK_VILLAGE_CODES.TK.name),
    ).not.toBeInTheDocument();
  });

  // IncludeHidden = true
  it("shows all village codes when hidden checkbox is checked", async () => {
    const user = userEvent.setup();

    renderWithQueryClient(<VillageCodesPage />);

    const showHiddenCheckbox = screen.getByLabelText("Show Hidden");
    await user.click(showHiddenCheckbox);

    await waitFor(() => {
      // Should show all village codes including hidden ones
      expect(screen.getByText(MOCK_VILLAGE_CODES.PC.code)).toBeInTheDocument();
      expect(screen.getByText(MOCK_VILLAGE_CODES.PC.name)).toBeInTheDocument();
      expect(screen.getByText(MOCK_VILLAGE_CODES.CA.code)).toBeInTheDocument();
      expect(screen.getByText(MOCK_VILLAGE_CODES.CA.name)).toBeInTheDocument();
      expect(screen.getByText(MOCK_VILLAGE_CODES.TK.code)).toBeInTheDocument();
      expect(screen.getByText(MOCK_VILLAGE_CODES.TK.name)).toBeInTheDocument();
    });
  });

  // IncludeHidden = false
  it("hides village codes again when hidden checkbox is unchecked", async () => {
    const user = userEvent.setup();

    renderWithQueryClient(<VillageCodesPage />);

    const showHiddenCheckbox = screen.getByLabelText("Show Hidden");

    await user.click(showHiddenCheckbox);
    await waitFor(() => {
      expect(screen.getByText(MOCK_VILLAGE_CODES.TK.code)).toBeInTheDocument();
    });

    await user.click(showHiddenCheckbox);
    await waitFor(() => {
      expect(
        screen.queryByText(MOCK_VILLAGE_CODES.TK.code),
      ).not.toBeInTheDocument();
      expect(
        screen.queryByText(MOCK_VILLAGE_CODES.TK.name),
      ).not.toBeInTheDocument();
    });

    // Visible ones should still be there
    expect(screen.getByText(MOCK_VILLAGE_CODES.PC.code)).toBeInTheDocument();
    expect(screen.getByText(MOCK_VILLAGE_CODES.CA.code)).toBeInTheDocument();
  });
});
