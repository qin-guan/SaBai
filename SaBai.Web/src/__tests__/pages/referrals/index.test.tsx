import { cleanup, screen, within } from "@testing-library/react";
import ReferralsPage from "@/pages/referrals";
import {
  assertBreadcrumbs,
  assertLoadingSpinner,
} from "@/__tests__/utils/helper-functions";
import { renderWithQueryClient } from "@/__tests__/utils/renderWithQueryClient";

// Override only the list query factory; the `...QueryKey` helpers stay real.
const mocks = vi.hoisted(() => ({
  list: { data: undefined as unknown, loading: false, error: false },
}));

vi.mock("@sabai/sdk/hooks", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@sabai/sdk/hooks")>();
  return {
    ...actual,
    saBaiAPIEndpointsReferralsListGetEndpointQueryOptions: () => ({
      queryKey: actual.saBaiAPIEndpointsReferralsListGetEndpointQueryKey(),
      queryFn: () =>
        mocks.list.loading
          ? new Promise(() => {})
          : mocks.list.error
            ? Promise.reject(new Error("failed"))
            : Promise.resolve(mocks.list.data),
      staleTime: Infinity,
    }),
  };
});

vi.mock("@/components/PatientPhoto", () => ({
  PatientPhoto: () => <span data-testid="patient-photo" />,
}));

const MOCK_REFERRALS = [
  {
    id: "11111111-0000-4000-8000-000000000011",
    referredFor: "Chronic",
    referralNotes: "Follow up",
    referralState: "New",
    referralOutcome: null,
    consultId: "c0000000-0000-4000-8000-000000000005",
    createdAt: "2026-10-12T09:00:00",
    consultDate: "2026-10-12T08:30:00",
    doctorId: "d0000000-0000-4000-8000-000000000001",
    patientId: "a0000000-0000-4000-8000-000000000012",
    patientName: "Alice Tan",
    hasPhoto: true,
    villageCodeId: "b0000000-0000-4000-8000-000000000001",
    villageCode: "SB",
    villageColorHex: "#C0392B",
  },
  {
    id: "22222222-0000-4000-8000-000000000010",
    referredFor: "Acute",
    referralNotes: null,
    referralState: "Seen",
    referralOutcome: null,
    consultId: "c0000000-0000-4000-8000-000000000004",
    createdAt: "2026-10-11T09:00:00",
    consultDate: "2026-10-11T08:30:00",
    doctorId: "d0000000-0000-4000-8000-000000000001",
    patientId: "a0000000-0000-4000-8000-000000000034",
    patientName: "Bob Lim",
    hasPhoto: false,
    villageCodeId: "b0000000-0000-4000-8000-000000000002",
    villageCode: "PC",
    villageColorHex: "#2E86C1",
  },
];

function getBodyRows() {
  const table = screen.getByRole("table");
  return within(table).getAllByRole("row").slice(1);
}

describe("ReferralsPage", () => {
  beforeEach(() => {
    mocks.list = { data: MOCK_REFERRALS, loading: false, error: false };
  });

  afterEach(() => {
    cleanup();
  });

  it("renders the title and breadcrumbs", async () => {
    // Given - the referrals page is opened
    renderWithQueryClient(<ReferralsPage />);
    // Then - the title and Home / Referrals breadcrumbs show
    expect(
      await screen.findByRole("heading", { name: "Referrals" }),
    ).toBeInTheDocument();
    assertBreadcrumbs(["Home", "Referrals"]);
  });

  it("shows a loading spinner while referrals load", () => {
    // Given - the referral list is still loading
    mocks.list = { data: undefined, loading: true, error: false };
    renderWithQueryClient(<ReferralsPage />);
    // Then - a loading spinner shows
    assertLoadingSpinner("Loading referrals...");
  });

  it("shows an error message when loading fails", async () => {
    // Given - the referral list failed to load
    mocks.list = { data: undefined, loading: false, error: true };
    renderWithQueryClient(<ReferralsPage />);
    // Then - an error message shows
    expect(
      await screen.findByText("An error has occurred!"),
    ).toBeInTheDocument();
  });

  it("explains where referrals come from when there are none", async () => {
    // Given - there are no referrals yet
    mocks.list = { data: [], loading: false, error: false };
    renderWithQueryClient(<ReferralsPage />);
    // Then - the empty state says referrals come from consults
    expect(
      await screen.findByText(
        "No referrals yet. Referrals made during consults will appear here.",
      ),
    ).toBeInTheDocument();
  });

  it("lists each referral with its patient summary and actions", async () => {
    // Given - two referrals exist
    renderWithQueryClient(<ReferralsPage />);
    // Then - each gets a row with patient name, referral reason, links and status
    await screen.findByRole("table");
    const rows = getBodyRows();
    expect(rows).toHaveLength(2);

    const first = within(rows[0]);
    expect(first.getByText("Alice Tan")).toBeInTheDocument();
    expect(first.getByText("Referred for: Chronic")).toBeInTheDocument();
    expect(
      first.getByRole("link", { name: "Referral Details" }),
    ).toHaveAttribute(
      "href",
      "/referrals/11111111-0000-4000-8000-000000000011",
    );
    expect(
      first.getByRole("link", { name: "Go to patient's consults" }),
    ).toHaveAttribute("href", "/consults/a0000000-0000-4000-8000-000000000012");
    expect(
      first.getByRole("combobox", { name: "Referral status" }),
    ).toHaveValue("New");
    expect(
      within(rows[1]).getByRole("combobox", { name: "Referral status" }),
    ).toHaveValue("Seen");
  });
});
