import { screen } from "@testing-library/react";
import { VisitSummaryPanel } from "@/components/consults/VisitSummaryPanel";
import { renderWithQueryClient } from "@/__tests__/utils/renderWithQueryClient";

const mocks = vi.hoisted(() => ({
  vitals: undefined as unknown,
  eyesight: undefined as unknown,
}));

const respond = (data: unknown) =>
  data ? Promise.resolve(data) : Promise.reject(new Error("not found"));

vi.mock("@sabai/sdk/hooks", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@sabai/sdk/hooks")>();
  return {
    ...actual,
    saBaiAPIEndpointsVitalsGetByVisitGetEndpointQueryOptions: () => ({
      queryKey: ["vitals"],
      queryFn: () => respond(mocks.vitals),
    }),
    saBaiAPIEndpointsEyesightGetByVisitGetEndpointQueryOptions: () => ({
      queryKey: ["eyesight"],
      queryFn: () => respond(mocks.eyesight),
    }),
  };
});

describe("VisitSummaryPanel", () => {
  beforeEach(() => {
    mocks.vitals = undefined;
    mocks.eyesight = undefined;
  });

  it("shows grouped vitals with diabetes highlighted", async () => {
    mocks.vitals = {
      height: 170,
      weight: 65,
      systolic: 120,
      diastolic: 80,
      diabetesMellitus: true,
      urineTest: "Trace protein",
      others: "None",
    };

    renderWithQueryClient(<VisitSummaryPanel visitId={7} />);

    expect(await screen.findByText("Anthropometric")).toBeInTheDocument();
    expect(screen.getByText("Cardiovascular / General")).toBeInTheDocument();
    expect(screen.getByText("Metabolic")).toBeInTheDocument();
    expect(screen.getByText("Urinalysis Diagnostics")).toBeInTheDocument();
    expect(screen.getByText("Additional Clinical Remarks")).toBeInTheDocument();
    expect(screen.getByText("Yes")).toHaveClass("text-red-600");
  });

  it("shows empty states when nothing is recorded", async () => {
    renderWithQueryClient(<VisitSummaryPanel visitId={7} />);

    expect(
      await screen.findByText("No vitals recorded for this visit."),
    ).toBeInTheDocument();
    expect(
      screen.getByText("No vision recorded for this visit."),
    ).toBeInTheDocument();
  });
});
