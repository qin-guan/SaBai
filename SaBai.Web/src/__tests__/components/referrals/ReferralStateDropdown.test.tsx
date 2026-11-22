import { Toaster, toast } from "react-hot-toast";
import userEvent from "@testing-library/user-event";
import { cleanup, screen, waitFor } from "@testing-library/react";
import { QueryClient } from "@tanstack/react-query";
import { ReferralStateDropdown } from "@/components/referrals/ReferralStateDropdown";
import { REFERRAL_STATES } from "@/lib/constants/referralStates";
import {
  createTestQueryClient,
  renderWithQueryClient,
} from "@/__tests__/utils/renderWithQueryClient";

const REFERRAL_ID = "7b1c2f3e-0000-4000-8000-000000000007";

// Override only the mutation factory; the `...QueryKey` helpers stay real.
const mocks = vi.hoisted(() => ({ updateState: vi.fn() }));

vi.mock("@sabai/sdk/hooks", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@sabai/sdk/hooks")>();
  return {
    ...actual,
    saBaiAPIEndpointsReferralsUpdateStatePatchEndpointMutationOptions: () => ({
      mutationKey: ["referral-update-state"],
      mutationFn: mocks.updateState,
    }),
  };
});

let queryClient: QueryClient;

function renderDropdown() {
  return renderWithQueryClient(
    <>
      <ReferralStateDropdown referralId={REFERRAL_ID} referralState="New" />
      <Toaster />
    </>,
    { queryClient },
  );
}

describe("ReferralStateDropdown", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    toast.remove();
    queryClient = createTestQueryClient();
    mocks.updateState.mockResolvedValue({});
  });

  afterEach(() => {
    cleanup();
  });

  it("shows every referral state with the current one selected", () => {
    // Given - a referral whose current state is New
    renderDropdown();
    // Then - every state is an option, in lifecycle order, with New selected
    const select = screen.getByRole("combobox", { name: "Referral status" });
    expect(select).toHaveValue("New");
    expect(screen.getAllByRole("option").map((o) => o.textContent)).toEqual([
      ...REFERRAL_STATES,
    ]);
  });

  it("saves the new state without sending an outcome", async () => {
    // Given - the dropdown for the referral
    renderDropdown();
    // When - the user picks Seen
    await userEvent.selectOptions(
      screen.getByRole("combobox", { name: "Referral status" }),
      "Seen",
    );
    // Then - only the id and state are sent, so the saved outcome is left untouched
    await waitFor(() =>
      expect(mocks.updateState).toHaveBeenCalledWith(
        {
          path: { id: REFERRAL_ID },
          body: { referralState: "Seen" },
        },
        expect.anything(),
      ),
    );
  });

  it("is disabled while the update is saving", async () => {
    // Given - a status update that never finishes
    mocks.updateState.mockReturnValue(new Promise(() => {}));
    renderDropdown();
    // When - the user picks Seen
    await userEvent.selectOptions(
      screen.getByRole("combobox", { name: "Referral status" }),
      "Seen",
    );
    // Then - the dropdown cannot be changed again until it finishes
    await waitFor(() =>
      expect(
        screen.getByRole("combobox", { name: "Referral status" }),
      ).toBeDisabled(),
    );
  });

  it("refreshes referral queries and confirms on success", async () => {
    // Given - the dropdown for the referral
    const invalidate = vi.spyOn(queryClient, "invalidateQueries");
    renderDropdown();
    // When - the status update succeeds
    await userEvent.selectOptions(
      screen.getByRole("combobox", { name: "Referral status" }),
      "Seen",
    );
    // Then - the list and this referral's detail are refetched and a success toast shows
    expect(
      await screen.findByText("Referral status updated"),
    ).toBeInTheDocument();
    expect(invalidate).toHaveBeenCalledWith({
      queryKey: [{ url: "/referrals" }],
    });
    expect(invalidate).toHaveBeenCalledWith({
      queryKey: [{ url: "/referrals/:id", params: { id: REFERRAL_ID } }],
    });
  });

  it("shows an error toast when the update fails", async () => {
    // Given - the status update will fail
    mocks.updateState.mockRejectedValue(new Error("boom"));
    renderDropdown();
    // When - the user picks Seen
    await userEvent.selectOptions(
      screen.getByRole("combobox", { name: "Referral status" }),
      "Seen",
    );
    // Then - the user is told it failed
    expect(await screen.findByText("boom")).toBeInTheDocument();
  });
});
