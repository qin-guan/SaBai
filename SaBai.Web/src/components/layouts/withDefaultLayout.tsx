// src/components/layouts/withDefaultLayout.tsx
import { PatientsProvider } from "@/lib/context/PatientsContext";
import { VillageCodeProvider } from "@/lib/context/VillageCodeContext";
import SidebarLayout from "./SidebarLayout";
import type { ReactNode } from "react";

const withDefaultLayout = (page: ReactNode) => (
  <VillageCodeProvider>
    <PatientsProvider>
      <SidebarLayout>{page}</SidebarLayout>
    </PatientsProvider>
  </VillageCodeProvider>
);

export default withDefaultLayout;
