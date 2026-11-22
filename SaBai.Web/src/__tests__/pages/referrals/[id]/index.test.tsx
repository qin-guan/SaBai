import { Toaster, toast } from "react-hot-toast";
import userEvent from "@testing-library/user-event";
import { cleanup, screen, waitFor } from "@testing-library/react";
import { useRouter } from "next/router";
import ReferralDetailsPage from "@/pages/referrals/[id]";
import {
  assertBreadcrumbs,
  assertLoadingSpinner,
} from "@/__tests__/utils/helper-functions";
import { renderWithQueryClient } from "@/__tests__/utils/renderWithQueryClient";

const REFERRAL_ID = "11111111-0000-4000-8000-000000000011";
const PATIENT_ID = "a0000000-0000-4000-8000-000000000012";

vi.mock("next/router", () => ({
  useRouter: vi.fn(),
}));

// Override the option factories the page consumes; the `...QueryKey` helpers
// stay real.
const mocks = vi.hoisted(() => ({
  referral: { data: undefined as unknown, loading: false, error: false },
  updateOutcome: vi.fn(),
}));

vi.mock("@sabai/sdk/hooks", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@sabai/sdk/hooks")>();
  return {
    ...actual,
    saBaiAPIEndpointsReferralsGetEndpointQueryOptions: () => ({
      queryKey: ["referral"],
      queryFn: () =>
        mocks.referral.loading
          ? new Promise(() => {})
          : mocks.referral.error
            ? Promise.reject(new Error("failed"))
            : Promise.resolve(mocks.referral.data),
      staleTime: Infinity,
    }),
    saBaiAPIEndpointsReferralsUpdateOutcomePatchEndpointMutationOptions:
      () => ({
        mutationKey: ["referral-update-outcome"],
        mutationFn: mocks.updateOutcome,
      }),
    saBaiAPIEndpointsReferralsUpdateStatePatchEndpointMutationOptions: () => ({
      mutationKey: ["referral-update-state"],
      mutationFn: vi.fn(),
    }),
  };
});

vi.mock("@/components/PatientPhoto", () => ({
  PatientPhoto: () => <span data-testid="patient-photo" />,
}));

const MOCK_REFERRAL = {
  id: REFERRAL_ID,
  referredFor: "Chronic",
  referralNotes: "Follow up",
  referralState: "Seen",
  referralOutcome: "Seen by specialist",
  consultId: "c0000000-0000-4000-8000-000000000005",
  createdAt: "2026-10-12T09:00:00",
  consultDate: "2026-10-12T08:30:00",
  patientId: PATIENT_ID,
  patientName: "Alice Tan",
  hasPhoto: true,
  villageCode: "SB",
  villageColorHex: "#C0392B",
};

function renderPage() {
  return renderWithQueryClient(
    <>
      <ReferralDetailsPage />
      <Toaster />
    </>,
  );
}

describe("ReferralDetailsPage", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    toast.remove();
    vi.mocked(useRouter).mockReturnValue({
      isReady: true,
      query: { id: REFERRAL_ID },
    } as unknown as ReturnType<typeof useRouter>);
    mocks.referral = { data: MOCK_REFERRAL, loading: false, error: false };
    mocks.updateOutcome.mockResolvedValue({});
  });

  afterEach(() => {
    cleanup();
  });

  it("shows a loading spinner while the referral loads", () => {
    // Given - the referral is still loading
    mocks.referral = { data: undefined, loading: true, error: false };
    renderPage();
    // Then - a loading spinner shows
    assertLoadingSpinner("Loading referral...");
  });

  it("shows an error message when loading fails", async () => {
    // Given - the referral failed to load
    mocks.referral = { data: undefined, loading: false, error: true };
    renderPage();
    // Then - an error message shows
    expect(
      await screen.findByText("An error has occurred!"),
    ).toBeInTheDocument();
  });

  it("shows the referral details with breadcrumbs", async () => {
    // Given - a referral exists
    renderPage();
    // Then - its details, status and consult link show
    expect(
      await screen.findByRole("heading", { name: "Referral Details" }),
    ).toBeInTheDocument();
    assertBreadcrumbs(["Home", "Referrals", "Referral Details"]);
    expect(screen.getByText("Chronic")).toBeInTheDocument();
    expect(screen.getByText("Follow up")).toBeInTheDocument();
    expect(screen.getByText("Seen by specialist")).toBeInTheDocument();
    expect(
      screen.getByRole("combobox", { name: "Referral status" }),
    ).toHaveValue("Seen");
    expect(
      screen.getByRole("link", { name: "Go to patient's consults" }),
    ).toHaveAttribute("href", `/consults/${PATIENT_ID}`);
  });

  it("saves only the outcome when it is edited", async () => {
    // Given - the referral details are shown
    renderPage();
    await userEvent.click(
      await screen.findByRole("button", { name: "Edit outcome" }),
    );
    // When - the user changes the outcome and saves
    const textarea = screen.getByRole("textbox", { name: "Referral outcome" });
    await userEvent.clear(textarea);
    await userEvent.type(textarea, "  Treated  ");
    await userEvent.click(screen.getByRole("button", { name: "Save" }));
    // Then - the outcome endpoint gets the trimmed outcome and the user is told
    await waitFor(() =>
      expect(mocks.updateOutcome).toHaveBeenCalledWith(
        {
          path: { id: REFERRAL_ID },
          body: { referralOutcome: "Treated" },
        },
        expect.anything(),
      ),
    );
    expect(
      await screen.findByText("Referral outcome saved"),
    ).toBeInTheDocument();
  });

  it("shows an error toast when saving the outcome fails", async () => {
    // Given - saving the outcome will fail
    mocks.updateOutcome.mockRejectedValue(new Error("boom"));
    renderPage();
    await userEvent.click(
      await screen.findByRole("button", { name: "Edit outcome" }),
    );
    // When - the user saves
    await userEvent.click(screen.getByRole("button", { name: "Save" }));
    // Then - the error is shown and the editor stays open
    expect(await screen.findByText("boom")).toBeInTheDocument();
    expect(
      screen.getByRole("textbox", { name: "Referral outcome" }),
    ).toBeInTheDocument();
  });
});
