import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import DeleteConfirmModal from "@/components/medication-brands/DeleteConfirmModal";
import type { BrandRowProps } from "@/components/medication-brands/BrandRow";

const BASE_BRAND: BrandRowProps = {
  id: "33333333-3333-3333-3333-333333333333",
  name: "Panadol",
  activeIngredientId: "11111111-1111-1111-1111-111111111111",
  activeIngredientName: "Paracetamol",
  unitOfMeasurement: "mg",
  remarks: null,
};

function renderModal({
  brand = BASE_BRAND,
  onConfirm = vi.fn(),
  onCancel = vi.fn(),
}: {
  brand?: BrandRowProps;
  onConfirm?: () => void;
  onCancel?: () => void;
} = {}) {
  render(
    <DeleteConfirmModal
      brand={brand}
      onConfirm={onConfirm}
      onCancel={onCancel}
    />,
  );

  return { onConfirm, onCancel };
}

describe("DeleteConfirmModal", () => {
  describe("Rendering", () => {
    it("renders the title 'Confirm Deletion?'", () => {
      renderModal();

      expect(
        screen.getByRole("heading", {
          name: "Confirm Deletion?",
          hidden: true,
        }),
      ).toBeInTheDocument();
    });

    it("displays the brand name, active ingredient name, and unit", () => {
      renderModal();

      expect(screen.getByText("Panadol")).toBeInTheDocument();
      expect(screen.getByText("Paracetamol")).toBeInTheDocument();
      expect(screen.getByText("mg")).toBeInTheDocument();
    });

    it("displays remarks when provided", () => {
      renderModal({ brand: { ...BASE_BRAND, remarks: "Handle with care" } });

      expect(screen.getByText("Handle with care")).toBeInTheDocument();
    });

    it("renders the remarks row without a value when remarks are absent", () => {
      renderModal({ brand: { ...BASE_BRAND, remarks: null } });

      expect(screen.getByText("Remarks:")).toBeInTheDocument();
    });
  });

  describe("Initial render", () => {
    it("does not invoke onConfirm or onCancel on mount", () => {
      const { onConfirm, onCancel } = renderModal();

      expect(onConfirm).not.toHaveBeenCalled();
      expect(onCancel).not.toHaveBeenCalled();
    });
  });

  describe("Cancel button", () => {
    it("calls onCancel when clicked", async () => {
      const user = userEvent.setup();
      const { onConfirm, onCancel } = renderModal();

      await user.click(
        screen.getByRole("button", { name: "Cancel", hidden: true }),
      );

      expect(onCancel).toHaveBeenCalledOnce();
      expect(onConfirm).not.toHaveBeenCalled();
    });
  });

  describe("Confirm button", () => {
    it("calls onConfirm when clicked", async () => {
      const user = userEvent.setup();
      const { onConfirm, onCancel } = renderModal();

      await user.click(
        screen.getByRole("button", { name: "Confirm", hidden: true }),
      );

      expect(onConfirm).toHaveBeenCalledOnce();
      expect(onCancel).not.toHaveBeenCalled();
    });
  });
});
