import PatientVitalsPage from "@/pages/vitals/[id]";
import { Toaster, toast } from "react-hot-toast";
import { fireEvent, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { assertLoadingSpinner } from "@/__tests__/utils/helper-functions";
import { useRouter } from "next/router";
import { renderWithQueryClient } from "@/__tests__/utils/renderWithQueryClient";

vi.mock("next/router", () => ({
  useRouter: vi.fn(),
}));

vi.mock("@/components/layouts/withDefaultLayout", () => ({
  default: (page: React.ReactNode) => page,
}));

vi.mock("@/components/layouts/PatientTopMenuLayout", () => ({
  default: ({ children }: { children: React.ReactNode }) => <>{children}</>,
}));

// The page now uses the generated SDK hooks. Override the option factories it
// consumes; the `...QueryKey` helpers (used for invalidation) stay real.
const mocks = vi.hoisted(() => ({
  patient: { data: undefined as unknown, loading: false },
  visits: { data: undefined as unknown, loading: false },
  vitals: { data: undefined as unknown, loading: false },
  createVitals: vi.fn(),
  updateVitals: vi.fn(),
}));

vi.mock("@sabai/sdk/hooks", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@sabai/sdk/hooks")>();
  return {
    ...actual,
    saBaiAPIEndpointsPatientsGetEndpointQueryOptions: () => ({
      queryKey: ["patient"],
      queryFn: () =>
        mocks.patient.loading
          ? new Promise(() => {})
          : Promise.resolve(mocks.patient.data),
      initialData: mocks.patient.loading ? undefined : mocks.patient.data,
      staleTime: Infinity,
    }),
    saBaiAPIEndpointsPatientsVisitsGetEndpointQueryOptions: () => ({
      queryKey: ["patient-visits"],
      queryFn: () =>
        mocks.visits.loading
          ? new Promise(() => {})
          : Promise.resolve(mocks.visits.data),
      initialData: mocks.visits.loading ? undefined : mocks.visits.data,
      staleTime: Infinity,
    }),
    saBaiAPIEndpointsVitalsGetByVisitGetEndpointQueryOptions: () => ({
      queryKey: ["vitals"],
      queryFn: () =>
        mocks.vitals.loading
          ? new Promise(() => {})
          : Promise.resolve(mocks.vitals.data),
      initialData: mocks.vitals.loading ? undefined : mocks.vitals.data,
      staleTime: Infinity,
    }),
    saBaiAPIEndpointsVitalsCreatePostEndpointMutationOptions: () => ({
      mutationKey: ["vitals-create"],
      mutationFn: mocks.createVitals,
    }),
    saBaiAPIEndpointsVitalsUpdateByVisitPatchEndpointMutationOptions: () => ({
      mutationKey: ["vitals-update"],
      mutationFn: mocks.updateVitals,
    }),
  };
});

const mockUseRouter = useRouter as ReturnType<typeof vi.fn>;

const MOCK_PATIENT = {
  id: 1,
  name: "John Doe",
  dateOfBirth: new Date("2000-01-01"),
  gender: "male",
};

// Single visit to trigger the auto-select useEffect, so VitalsForm render without needing dropdown interaction
const MOCK_VISITS = [
  { id: 10, date: new Date("2024-06-15T10:30:00"), patientId: 1 },
];
const MOCK_VITALS = {
  visitId: 10,
  height: "170",
  weight: "65",
  temperature: "37.0",
  systolic: 120,
  diastolic: 80,
  heartRate: 72,
  hemocueCount: "14",
  diabetesMellitus: true,
  urineTest: "Clear",
  bloodGlucoseNonFasting: "5.5",
  bloodGlucoseFasting: "4.8",
  hba1c: "5.2",
  others: "No issues",
};

function renderPage() {
  return renderWithQueryClient(
    <>
      <Toaster />
      <PatientVitalsPage />
    </>,
  );
}

describe("VitalsForm", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    toast.remove();

    mockUseRouter.mockReturnValue({ query: { id: "1" }, isReady: true });

    // Set up patient and a single visit so VitalsForm auto-renders
    mocks.patient.data = MOCK_PATIENT;
    mocks.patient.loading = false;
    mocks.visits.data = { visits: MOCK_VISITS };
    mocks.visits.loading = false;
    mocks.vitals.data = null;
    mocks.vitals.loading = false;

    mocks.createVitals.mockResolvedValue(undefined);
    mocks.updateVitals.mockResolvedValue(undefined);
  });

  test("renders loading spinner while vitals data is loading", async () => {
    mocks.vitals.loading = true;
    mocks.vitals.data = undefined;

    renderPage();

    await waitFor(() => {
      assertLoadingSpinner("Loading Vitals");
    });
  });

  test("renders all form fields when no existing vitals", async () => {
    renderPage();

    await waitFor(() => {
      expect(screen.getByText("Height (cm)")).toBeInTheDocument();
      expect(screen.getByText("Weight (kg)")).toBeInTheDocument();
      expect(screen.getByText("Body Temperature (°C)")).toBeInTheDocument();
      expect(screen.getByText("Heart Rate (BPM)")).toBeInTheDocument();
      expect(
        screen.getByText("Diabetes Mellitus History Status Flag"),
      ).toBeInTheDocument();
      expect(
        screen.getByRole("button", { name: "Save Records" }),
      ).toBeInTheDocument();
    });
  });

  test("pre-fills form with existing vitals data", async () => {
    mocks.vitals.data = MOCK_VITALS;

    renderPage();

    await waitFor(() => {
      expect(screen.getByDisplayValue(MOCK_VITALS.height)).toBeInTheDocument();
      expect(screen.getByDisplayValue(MOCK_VITALS.weight)).toBeInTheDocument();
      expect(
        screen.getByDisplayValue(MOCK_VITALS.temperature),
      ).toBeInTheDocument();
    });
  });

  test("shows success toast when vitals are saved", async () => {
    const user = userEvent.setup();

    renderPage();

    await waitFor(() => {
      expect(screen.getByLabelText("Height (cm)")).toBeInTheDocument();
    });

    // Dirty the form so isDirty guard doesn't block submission, value here to be different from what is in MOCK_VITALS
    await user.type(screen.getByLabelText("Height (cm)"), "171");
    await user.click(screen.getByRole("button", { name: "Save Records" }));

    await waitFor(() => {
      const toast = screen.getByRole("status");
      expect(toast).toBeInTheDocument();
      expect(toast.textContent).toBe("Vitals saved successfully!");
    });

    // No existing vitals -> POST create
    expect(mocks.createVitals).toHaveBeenCalledWith({
      body: expect.objectContaining({ visitId: 10, height: 171 }),
    });
  });

  test("clears a previously-saved field by sending null on update", async () => {
    const user = userEvent.setup();
    mocks.vitals.data = MOCK_VITALS;

    renderPage();

    const tempInput = await screen.findByDisplayValue(MOCK_VITALS.temperature);
    await user.clear(tempInput);

    fireEvent.submit(tempInput.closest("form")!);

    // A cleared field must persist as null (explicit NULL), not be dropped.
    await waitFor(() => {
      expect(mocks.updateVitals).toHaveBeenCalledWith({
        path: { visitId: 10 },
        body: expect.objectContaining({ temperature: null }),
      });
    });
  });

  test("shows info toast when submitting without changing any field", async () => {
    const user = userEvent.setup();

    renderPage();

    await waitFor(() => {
      expect(
        screen.getByRole("button", { name: "Save Records" }),
      ).toBeInTheDocument();
    });

    await user.click(screen.getByRole("button", { name: "Save Records" }));

    await waitFor(() => {
      const toast = screen.getByRole("status");
      expect(toast).toBeInTheDocument();
      expect(toast.textContent).toBe("No form field changed!");
    });
  });
});
