import Modal from "@/components/interactive/Modal";
import { Button } from "@/components/interactive/Button/Button";
import type { BrandRowProps } from "./BrandRow";

export default function DeleteConfirmModal({
  brand,
  onConfirm,
  onCancel,
}: {
  brand: BrandRowProps;
  onConfirm: () => void;
  onCancel: () => void;
}) {
  return (
    <Modal title="Confirm Deletion?" onClose={onCancel}>
      <table>
        <tbody>
          <tr>
            <td>Brand:</td>
            <td>{brand.name}</td>
          </tr>
          <tr>
            <td className="pr-4">Active Ingredient:</td>
            <td>{brand.activeIngredientName}</td>
          </tr>
          <tr>
            <td>Unit:</td>
            <td>{brand.unitOfMeasurement}</td>
          </tr>
          <tr>
            <td className="align-top">Remarks:</td>
            <td className="whitespace-normal break-words">{brand.remarks}</td>
          </tr>
        </tbody>
      </table>

      <div className="flex flex-row gap-2">
        <Button
          onClick={onCancel}
          colour="red"
          title="Cancel"
          className="w-full"
        />
        <Button
          onClick={onConfirm}
          colour="emerald"
          title="Confirm"
          className="w-full"
        />
      </div>
    </Modal>
  );
}
