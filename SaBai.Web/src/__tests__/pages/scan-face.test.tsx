import { cleanup, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { assertBreadcrumbs } from "@/__tests__/utils/helper-functions";
import { Toaster, toast } from "react-hot-toast";
import ScanFacePage from "@/pages/scan-face";
import Webcam from "react-webcam";
import { VillageCodeProvider } from "@/lib/context/VillageCodeContext";
import { renderWithQueryClient } from "@/__tests__/utils/renderWithQueryClient";

// The page's children use the generated SDK hooks for queries/mutations and the
// raw SDK clients for the face search / photo+face enrollment calls.
type SearchMatch = {
  id: number;
  name: string;
  displayId: string;
  villageCode: string | null;
  villageColorHex: string | null;
  hasPhoto: boolean;
  similarity: number;
};

const mocks = vi.hoisted(() => ({
  villageCodes: { data: [] as unknown, loading: false },
  createPatient: vi.fn(),
  searchMatches: [] as SearchMatch[],
  createVisit: vi.fn(),
  uploadPhoto: vi.fn(),
  enrollFace: vi.fn(),
}));

vi.mock("@sabai/sdk/hooks", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@sabai/sdk/hooks")>();
  return {
    ...actual,
    saBaiAPIEndpointsVillageCodesListGetEndpointQueryOptions: () => ({
      queryKey: ["village-codes", { includeHidden: false }],
      queryFn: () =>
        mocks.villageCodes.loading
          ? new Promise(() => {})
          : Promise.resolve({ villageCodes: mocks.villageCodes.data }),
      initialData: mocks.villageCodes.loading
        ? undefined
        : { villageCodes: mocks.villageCodes.data },
      staleTime: Infinity,
    }),
    saBaiAPIEndpointsPatientsCreatePostEndpointMutationOptions: () => ({
      mutationKey: ["patients-create"],
      mutationFn: mocks.createPatient,
    }),
  };
});

vi.mock("@sabai/sdk/clients", () => ({
  saBaiAPIEndpointsPatientsSearchByFacePostEndpoint: () => ({
    unwrap: async () => ({ matches: mocks.searchMatches }),
  }),
  saBaiAPIEndpointsVisitsCreatePostEndpoint: (...args: unknown[]) => {
    mocks.createVisit(...args);
    return { unwrap: async () => ({}) };
  },
  saBaiAPIEndpointsPatientsPhotoPutEndpoint: (...args: unknown[]) => {
    mocks.uploadPhoto(...args);
    return { unwrap: async () => ({}) };
  },
  saBaiAPIEndpointsPatientsFacesPostEndpoint: (...args: unknown[]) => {
    mocks.enrollFace(...args);
    return { unwrap: async () => ({}) };
  },
  saBaiAPIEndpointsPatientsPhotoGetEndpoint: () => ({
    unwrap: async () => null,
  }),
}));

vi.mock("@/lib/api/patientPhoto", async (importOriginal) => {
  const actual =
    await importOriginal<typeof import("@/lib/api/patientPhoto")>();
  return {
    ...actual,
    dataUrlToFile: vi.fn(
      async () => new File(["x"], "capture.jpg", { type: "image/jpeg" }),
    ),
  };
});

const router = vi.hoisted(() => ({ push: vi.fn() }));

vi.mock("next/router", () => ({
  useRouter: () => ({
    push: router.push,
    replace: vi.fn(),
    back: vi.fn(),
    prefetch: vi.fn(),
    query: {},
    pathname: "/scan-face",
    asPath: "/scan-face",
    isReady: true,
  }),
}));

// instead of data:image/jpeg;base64,qioweqowiejioqwjeoiqwoeojqwe...
// this is the imgSrc variable that the codebase uses after webcam capture
// test libaries can't do this because there's no live video feed, so we
// overwrite the prototype here so that imgSrc is flipped
// to an actual string to simulate picture taking
Webcam.prototype.getScreenshot = () =>
  "data:image/jpeg;base64,mock-string-data";

// jsdom has no `navigator.mediaDevices`, so the real `componentDidMount` would
// report a media error and leave the Capture button disabled. Simulate a live
// stream so the button becomes ready and capture can be exercised.
Webcam.prototype.componentDidMount = function (this: Webcam) {
  this.props.onUserMedia({} as MediaStream);
};

const VILLAGE_CODES = [
  { id: 1, name: "Village Alpha", colorHex: "", isVisible: true },
  { id: 2, name: "Village Beta", colorHex: "", isVisible: true },
];

async function fillValidRegistrationForm(container: HTMLElement) {
  const user = userEvent.setup();
  await user.type(container.querySelector('input[name="name"]')!, "Valid name");
  await user.type(
    container.querySelector('input[name="identificationNumber"]')!,
    "Valid id",
  );
  await user.type(
    container.querySelector('input[name="contactNo"]')!,
    "1234567",
  );
  await user.type(
    container.querySelector('input[name="drugAllergy"]')!,
    "drug allergies",
  );
  await user.type(
    container.querySelector('input[name="dateOfBirth"]')!,
    "2000-10-10",
  );
  await user.click(container.querySelector('button[name="gender-dropdown-button"]')!);
  await user.click(
    container.querySelector('button[name="gender-male-dropdown-option"]')!,
  );
}

describe("ScanFacePage", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    toast.dismissAll();
    window.localStorage.clear();

    mocks.villageCodes.data = [];
    mocks.villageCodes.loading = false;
    mocks.createPatient.mockResolvedValue({ id: 1 });
    mocks.searchMatches = [];
    mocks.createVisit.mockResolvedValue(undefined);
    mocks.uploadPhoto.mockResolvedValue(undefined);
    mocks.enrollFace.mockResolvedValue(undefined);
  });

  afterEach(() => {
    cleanup();
  });

  it("renders page title", () => {
    renderWithQueryClient(<ScanFacePage />);
    expect(
      screen.getByRole("heading", { name: "Scan Face" }),
    ).toBeInTheDocument();
  });

  it("renders breadcrumbs", () => {
    renderWithQueryClient(<ScanFacePage />);
    assertBreadcrumbs(["Home", "Scan Face"]);
  });

  it("displays a webcam and a form in registration mode", async () => {
    const user = userEvent.setup();

    mocks.villageCodes.data = VILLAGE_CODES;

    const { container } = renderWithQueryClient(
      <VillageCodeProvider>
        <ScanFacePage />
      </VillageCodeProvider>,
    );
    const captureButton = screen.getByRole("button", { name: "Capture" });

    await user.click(captureButton);

    await screen.findByText("Face matches");

    await user.click(
      screen.getByRole("button", { name: "Register New Patient" }),
    );

    expect(container.querySelector('input[name="name"]')).toBeInTheDocument();
    expect(
      container.querySelector('input[name="identificationNumber"]'),
    ).toBeInTheDocument();
    expect(
      container.querySelector('input[name="contactNo"]'),
    ).toBeInTheDocument();
    expect(
      container.querySelector('input[name="drugAllergy"]'),
    ).toBeInTheDocument();

    expect(screen.getByText("Has POOR Card?")).toBeInTheDocument();
    expect(screen.getByText("Has BS2 Card?")).toBeInTheDocument();
    expect(screen.getByText("Has Sabai Card?")).toBeInTheDocument();

    expect(
      screen.getByRole("button", { name: "Create New Patient" }),
    ).toBeInTheDocument();
    expect(
      screen.getByRole("button", { name: "Match Instead" }),
    ).toBeInTheDocument();
  });

  it("shows no matches and an option to register when the face search finds nothing", async () => {
    const user = userEvent.setup();

    renderWithQueryClient(
      <VillageCodeProvider>
        <ScanFacePage />
      </VillageCodeProvider>,
    );

    await user.click(screen.getByRole("button", { name: "Capture" }));

    expect(await screen.findByText("No matching patients found.")).toBeInTheDocument();
    expect(
      screen.getByRole("button", { name: "Register New Patient" }),
    ).toBeInTheDocument();
  });

  it("lists face-search matches with their photo and similarity", async () => {
    const user = userEvent.setup();

    mocks.searchMatches = [
      {
        id: 42,
        name: "Jane Doe",
        displayId: "V00042",
        villageCode: "V0",
        villageColorHex: "#123456",
        hasPhoto: false,
        similarity: 0.87,
      },
    ];

    renderWithQueryClient(
      <VillageCodeProvider>
        <ScanFacePage />
      </VillageCodeProvider>,
    );

    await user.click(screen.getByRole("button", { name: "Capture" }));

    expect(await screen.findByText("Jane Doe")).toBeInTheDocument();
    expect(screen.getByText(/87% match/)).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Select" })).toBeInTheDocument();
  });

  it("creates a visit for the selected match when a village code is set", async () => {
    const user = userEvent.setup();

    window.localStorage.setItem(
      "villageCodeState",
      JSON.stringify({ selectedVillageCodeId: 1 }),
    );
    mocks.villageCodes.data = VILLAGE_CODES;
    mocks.searchMatches = [
      {
        id: 42,
        name: "Jane Doe",
        displayId: "V00042",
        villageCode: "V0",
        villageColorHex: null,
        hasPhoto: false,
        similarity: 0.9,
      },
    ];

    renderWithQueryClient(
      <VillageCodeProvider>
        <ScanFacePage />
        <Toaster />
      </VillageCodeProvider>,
    );

    await user.click(screen.getByRole("button", { name: "Capture" }));
    await user.click(await screen.findByRole("button", { name: "Select" }));

    await waitFor(() => expect(mocks.createVisit).toHaveBeenCalledTimes(1));
    expect(mocks.createVisit).toHaveBeenCalledWith(
      expect.objectContaining({
        body: { patientId: 42, villageCodeId: 1 },
      }),
    );
  });

  it("displays an error if a village code is not selected while creating a patient", async () => {
    const user = userEvent.setup();

    const { container } = renderWithQueryClient(
      <VillageCodeProvider>
        <ScanFacePage />
        <Toaster />
      </VillageCodeProvider>,
    );

    await user.click(screen.getByRole("button", { name: "Capture" }));
    await screen.findByText("Face matches");
    await user.click(
      screen.getByRole("button", { name: "Register New Patient" }),
    );

    await fillValidRegistrationForm(container);

    await user.click(
      screen.getByRole("button", { name: "Create New Patient" }),
    );

    expect(await screen.findByRole("status")).toHaveTextContent(
      "Please select a village code before submitting.",
    );
    expect(mocks.createPatient).not.toHaveBeenCalled();
  });

  it("displays an error if the registration form's button is clicked while an image is not captured", async () => {
    const user = userEvent.setup();

    const { container } = renderWithQueryClient(
      <VillageCodeProvider>
        <ScanFacePage />
        <Toaster />
      </VillageCodeProvider>,
    );

    await user.click(screen.getByRole("button", { name: "Capture" }));
    await screen.findByText("Face matches");
    await user.click(
      screen.getByRole("button", { name: "Register New Patient" }),
    );

    await fillValidRegistrationForm(container);

    await user.click(screen.getByRole("button", { name: "Retake Photo" }));
    await user.click(
      screen.getByRole("button", { name: "Create New Patient" }),
    );

    expect(await screen.findByRole("status")).toHaveTextContent(
      "Please capture a face image before submitting.",
    );
    expect(mocks.createPatient).not.toHaveBeenCalled();
  });

  it("stores the captured photo and enrolls a face after creating a patient", async () => {
    const user = userEvent.setup();

    window.localStorage.setItem(
      "villageCodeState",
      JSON.stringify({ selectedVillageCodeId: 1 }),
    );
    mocks.villageCodes.data = VILLAGE_CODES;

    const { container } = renderWithQueryClient(
      <VillageCodeProvider>
        <ScanFacePage />
        <Toaster />
      </VillageCodeProvider>,
    );

    await user.click(screen.getByRole("button", { name: "Capture" }));
    await screen.findByText("Face matches");
    await user.click(
      screen.getByRole("button", { name: "Register New Patient" }),
    );

    await fillValidRegistrationForm(container);
    await user.click(
      screen.getByRole("button", { name: "Create New Patient" }),
    );

    await waitFor(() => expect(mocks.uploadPhoto).toHaveBeenCalledTimes(1));
    expect(mocks.enrollFace).toHaveBeenCalledTimes(1);
    expect(mocks.uploadPhoto).toHaveBeenCalledWith(
      expect.objectContaining({ path: { id: 1 }, body: expect.anything() }),
    );
  });

  it("displays a webcam and a capture button upon entering the page", () => {
    renderWithQueryClient(<ScanFacePage />);
    const webcam = document.getElementById("webcam");
    const captureButton = screen.getByRole("button", { name: "Capture" });

    expect(webcam).toBeInTheDocument();
    expect(captureButton).toBeInTheDocument();
  });

  it("changes the button from 'Capture' to 'Retake Photo' when button is clicked", async () => {
    const user = userEvent.setup();
    renderWithQueryClient(
      <VillageCodeProvider>
        <ScanFacePage />
      </VillageCodeProvider>,
    );
    const captureButton = screen.getByRole("button", { name: "Capture" });
    await user.click(captureButton);

    const retakeButton = await screen.findByRole("button", {
      name: "Retake Photo",
    });
    expect(retakeButton).toBeInTheDocument();
  });

  it("changes the image element to a video element when the 'Retake Photo' button is clicked", async () => {
    const user = userEvent.setup();
    const { container } = renderWithQueryClient(
      <VillageCodeProvider>
        <ScanFacePage />
      </VillageCodeProvider>,
    );
    const captureButton = screen.getByRole("button", { name: "Capture" });

    expect(
      container.querySelector(
        'img[src="data:image/jpeg;base64,mock-string-data"]',
      ),
    ).not.toBeInTheDocument();
    expect(document.getElementById("webcam")).toBeInTheDocument();

    await user.click(captureButton);

    await waitFor(() => {
      const capturedImg = container.querySelector(
        'img[src="data:image/jpeg;base64,mock-string-data"]',
      );
      expect(capturedImg).toBeInTheDocument();
      expect(document.getElementById("webcam")).not.toBeInTheDocument();
    });
  });

  it("disables the registration button and displays a loading message while registering patients", async () => {
    const user = userEvent.setup();

    window.localStorage.setItem(
      "villageCodeState",
      JSON.stringify({ selectedVillageCodeId: 1 }),
    );
    mocks.villageCodes.data = VILLAGE_CODES;
    // A never-resolving mutation keeps react-query's `isPending` true.
    mocks.createPatient.mockImplementation(() => new Promise(() => {}));

    const { container } = renderWithQueryClient(
      <VillageCodeProvider>
        <ScanFacePage />
        <Toaster />
      </VillageCodeProvider>,
    );

    await user.click(screen.getByRole("button", { name: "Capture" }));
    await screen.findByText("Face matches");
    await user.click(
      screen.getByRole("button", { name: "Register New Patient" }),
    );

    await fillValidRegistrationForm(container);

    await user.click(
      screen.getByRole("button", { name: "Create New Patient" }),
    );

    await waitFor(() => {
      const pendingButton = screen.getByRole("button", {
        name: "Creating new patient...",
      });
      expect(pendingButton).toBeDisabled();
    });
    expect(mocks.createPatient).toHaveBeenCalledWith(
      expect.objectContaining({ body: expect.anything() }),
    );
  });
});
