import { useQuery } from "@tanstack/react-query";
import {
  saBaiAPIEndpointsMedicationBrandsListGetEndpointQueryOptions,
  saBaiAPIEndpointsMedicationIngredientsListGetEndpointQueryOptions,
} from "@sabai/sdk/hooks";
import { apiConfig } from "@/lib/api/config";
import PageHeader from "@/components/PageHeader";
import LoadingSpinner from "@/components/LoadingSpinner";
import TableHeader from "@/components/TableHeader";
import BrandRow from "@/components/medication-brands/BrandRow";
import AddBrandModal from "@/components/medication-brands/AddBrandModal";

function Header() {
  return (
    <PageHeader
      breadcrumbs={[
        { label: "Home", href: "/" },
        { label: "Medication Stock", href: "/medication-stock" },
        { label: "Medication Brands" },
      ]}
      title="Medication Brands"
      description="Manage medication brands."
      actions={<AddBrandModal />}
    />
  );
}

function MedicationBrandsBasePage() {
  const brandsQuery = useQuery(
    saBaiAPIEndpointsMedicationBrandsListGetEndpointQueryOptions(apiConfig),
  );
  // the brands list only has the ingredient id, so the name and unit come from the ingredients list
  const ingredientsQuery = useQuery(
    saBaiAPIEndpointsMedicationIngredientsListGetEndpointQueryOptions(
      apiConfig,
    ),
  );

  const brands = brandsQuery.data;
  const ingredients = ingredientsQuery.data;
  const isLoading = brandsQuery.isLoading || ingredientsQuery.isLoading;
  const isError = brandsQuery.isError || ingredientsQuery.isError;

  function renderContent() {
    if (isError) {
      return <h1 className="text-red-500">An error has occurred!</h1>;
    }

    if (isLoading) {
      return <LoadingSpinner message="Loading brands..." className="p-12" />;
    }

    if (!brands || brands.length === 0) {
      return (
        <div className="p-12 text-center text-slate-500">
          No medication brands found. Seed the database or add a new record.
        </div>
      );
    }

    return (
      <div className="overflow-x-auto">
        <table className="min-w-full divide-y divide-slate-200">
          <TableHeader
            headers={[
              "Brand ID",
              "Brand Name",
              "Active Ingredient Name",
              "Unit of Measurement",
              "Remarks",
              "Actions",
            ]}
          />
          <tbody className="bg-white divide-y divide-slate-200">
            {brands.map((brand) => {
              const ingredient = ingredients?.find(
                (item) => item.id === brand.activeIngredientId,
              );
              return (
                <BrandRow
                  key={brand.id}
                  {...brand}
                  activeIngredientName={ingredient?.name ?? ""}
                  unitOfMeasurement={ingredient?.unitOfMeasurement ?? ""}
                />
              );
            })}
          </tbody>
        </table>
      </div>
    );
  }

  return (
    <div className="min-h-screen flex-1 p-8">
      <Header />
      {renderContent()}
    </div>
  );
}

export default MedicationBrandsBasePage;
