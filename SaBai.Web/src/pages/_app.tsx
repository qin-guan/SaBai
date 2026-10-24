import "@/styles/globals.css";
import type { AppProps } from "next/app";
import type { NextPage } from "next";
import type { ReactElement, ReactNode } from "react";
import { useState } from "react";
import { QueryClientProvider } from "@tanstack/react-query";
import { useRouter } from "next/router";
import withDefaultLayout from "@/components/layouts/withDefaultLayout";
import { Toaster } from "react-hot-toast";
import { makeQueryClient } from "@/lib/api/queryClient";

export type NextPageWithLayout = NextPage & {
  getLayout?: (page: ReactElement) => ReactNode;
};

type AppPropsWithLayout = AppProps & {
  Component: NextPageWithLayout;
};

function App({ Component, pageProps }: AppPropsWithLayout) {
  const router = useRouter();
  const [queryClient] = useState(makeQueryClient);

  /**
   * Force remounts only for patient routes to avoid stale-detail flashes when
   * navigating between dynamic patient pages (e.g. /patient/1 -> /patient/2).
   */
  const isPatientRoute = router.pathname.startsWith("/patient");

  const getLayout = Component.getLayout ?? withDefaultLayout;

  return (
    <QueryClientProvider client={queryClient}>
      {getLayout(
        <>
          <Component
            {...pageProps}
            key={isPatientRoute ? router.asPath : undefined}
          />
          <Toaster />
        </>,
      )}
    </QueryClientProvider>
  );
}

export default App;
