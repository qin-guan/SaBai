import { useState } from "react";
import { useRouter } from "next/router";
import Image from "next/image";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import {
  saBaiAPIEndpointsAuthLoginPostEndpointMutationOptions,
  saBaiAPIEndpointsAuthLoginRecoveryCodePostEndpointMutationOptions,
  saBaiAPIEndpointsAuthLoginTwoFactorPostEndpointMutationOptions,
} from "@sabai/sdk/hooks";
import type { SaBaiAPIEndpointsAuthSignInResponse } from "@sabai/sdk/types";
import { apiConfig } from "@/lib/api/config";
import { getApiErrorMessage, getApiErrorStatus } from "@/lib/api/errors";
import { paths } from "@/utils/paths";

type Step = "password" | "code" | "recovery";

function useLogin() {
  const router = useRouter();
  const queryClient = useQueryClient();
  const [step, setStep] = useState<Step>("password");
  const [userName, setUserName] = useState("");
  const [password, setPassword] = useState("");
  const [code, setCode] = useState("");
  const [rememberMachine, setRememberMachine] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const login = useMutation(
    saBaiAPIEndpointsAuthLoginPostEndpointMutationOptions(apiConfig),
  );
  const twoFactor = useMutation(
    saBaiAPIEndpointsAuthLoginTwoFactorPostEndpointMutationOptions(apiConfig),
  );
  const recovery = useMutation(
    saBaiAPIEndpointsAuthLoginRecoveryCodePostEndpointMutationOptions(apiConfig),
  );

  const submit = (): Promise<SaBaiAPIEndpointsAuthSignInResponse> => {
    if (step === "password") {
      return login.mutateAsync({ body: { userName, password } });
    }
    if (step === "recovery") {
      return recovery.mutateAsync({ body: { recoveryCode: code } });
    }
    return twoFactor.mutateAsync({ body: { code, rememberMachine } });
  };

  const handleSubmit = async (e: React.FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    setError(null);

    try {
      const result = await submit();
      if (result.requiresTwoFactor || !result.user) {
        // Password accepted; the account has two-factor on.
        setStep("code");
        setCode("");
        return;
      }

      queryClient.clear();
      const { redirectTo } = router.query;
      router.push(
        typeof redirectTo === "string" && redirectTo.startsWith("/")
          ? redirectTo
          : paths.home(),
      );
    } catch (err) {
      const status = getApiErrorStatus(err);
      if (step !== "password" && status === 401) {
        setError("That code is not valid.");
      } else if (status === 401) {
        setError("Invalid username or password.");
      } else {
        setError(getApiErrorMessage(err, "Unable to sign in."));
      }
    }
  };

  const reset = () => {
    setStep("password");
    setPassword("");
    setCode("");
    setError(null);
  };

  return {
    step,
    setStep,
    userName,
    setUserName,
    password,
    setPassword,
    code,
    setCode,
    rememberMachine,
    setRememberMachine,
    loading: login.isPending || twoFactor.isPending || recovery.isPending,
    error,
    handleSubmit,
    reset,
  };
}

function ErrorBanner({ message }: { message: string }) {
  return (
    <div className="rounded-lg bg-red-50 p-3.5 border border-red-100">
      <div className="flex gap-2.5">
        <svg
          className="mt-0.5 h-4 w-4 shrink-0 text-red-400"
          viewBox="0 0 20 20"
          fill="currentColor"
        >
          <path
            fillRule="evenodd"
            d="M10 18a8 8 0 100-16 8 8 0 000 16zM8.707 7.293a1 1 0 00-1.414 1.414L8.586 10l-1.293 1.293a1 1 0 101.414 1.414L10 11.414l1.293 1.293a1 1 0 001.414-1.414L11.414 10l1.293-1.293a1 1 0 00-1.414-1.414L10 8.586 8.707 7.293z"
            clipRule="evenodd"
          />
        </svg>
        <div>
          <p className="text-sm font-medium text-red-800">Login failed</p>
          <p className="mt-0.5 text-sm text-red-700">{message}</p>
        </div>
      </div>
    </div>
  );
}

const inputClass =
  "block w-full rounded-lg border border-slate-300 px-3 py-2.5 text-slate-900 placeholder:text-slate-400 focus:outline-none sm:text-sm";

export default function Login() {
  const {
    step,
    setStep,
    userName,
    setUserName,
    password,
    setPassword,
    code,
    setCode,
    rememberMachine,
    setRememberMachine,
    loading,
    error,
    handleSubmit,
    reset,
  } = useLogin();

  return (
    <div className="relative flex min-h-screen items-center justify-center">
      <Image
        src="/cambodia-image.webp"
        alt="Angkor Wat, Cambodia"
        fill
        sizes="100vw"
        className="object-cover"
        priority
      />
      <div className="absolute inset-0 bg-black/50" />

      <div className="relative z-10 w-full max-w-md px-4">
        <div className="rounded-2xl bg-white px-8 py-10 shadow-2xl">
          {/* Header */}
          <div className="mb-4 text-center">
            <Image
              src="/sabaiLogo.png"
              alt="Sabai logo"
              width={56}
              height={56}
              className="mx-auto mb-4"
            />
            <h2 className="text-2xl font-bold text-slate-900">Welcome</h2>
            <p className="mt-2 text-sm text-slate-500">
              {step === "password"
                ? "Log in to Sabai."
                : step === "code"
                  ? "Enter the 6-digit code from your authenticator app."
                  : "Enter one of your saved recovery codes."}
            </p>
          </div>

          {/* Form */}
          <form className="space-y-4" onSubmit={handleSubmit}>
            {step === "password" ? (
              <>
                <div>
                  <label
                    htmlFor="username"
                    className="block text-sm font-medium text-slate-700 mb-1"
                  >
                    Username or email
                  </label>
                  <input
                    id="username"
                    name="username"
                    autoComplete="username"
                    autoCapitalize="none"
                    required
                    value={userName}
                    onChange={(e) => setUserName(e.target.value)}
                    className={inputClass}
                  />
                </div>

                <div>
                  <label
                    htmlFor="password"
                    className="block text-sm font-medium text-slate-700 mb-1"
                  >
                    Password
                  </label>
                  <input
                    id="password"
                    name="password"
                    type="password"
                    autoComplete="current-password"
                    required
                    value={password}
                    onChange={(e) => setPassword(e.target.value)}
                    className={inputClass}
                    placeholder="••••••••"
                  />
                </div>
              </>
            ) : (
              <>
                <div>
                  <label
                    htmlFor="code"
                    className="block text-sm font-medium text-slate-700 mb-1"
                  >
                    {step === "code" ? "Authentication code" : "Recovery code"}
                  </label>
                  <input
                    id="code"
                    name="code"
                    inputMode={step === "code" ? "numeric" : "text"}
                    autoComplete="one-time-code"
                    autoCapitalize="none"
                    required
                    autoFocus
                    value={code}
                    onChange={(e) => setCode(e.target.value)}
                    className={`${inputClass} font-mono tracking-widest`}
                  />
                </div>
                {step === "code" && (
                  <label className="flex items-center gap-2 text-sm text-slate-700">
                    <input
                      type="checkbox"
                      checked={rememberMachine}
                      onChange={(e) => setRememberMachine(e.target.checked)}
                    />
                    Don&apos;t ask for a code on this device again
                  </label>
                )}
              </>
            )}

            {error && <ErrorBanner message={error} />}

            <button
              type="submit"
              disabled={loading}
              className="flex w-full justify-center rounded-lg bg-violet-600 px-3 py-2.5 text-sm font-semibold text-white hover:bg-violet-700 focus:outline-none disabled:opacity-50 disabled:cursor-not-allowed transition-colors duration-200"
            >
              {loading
                ? "Signing in..."
                : step === "password"
                  ? "Continue"
                  : "Verify"}
            </button>

            {step !== "password" && (
              <div className="flex justify-between text-sm">
                <button
                  type="button"
                  className="text-violet-700 hover:underline"
                  onClick={reset}
                >
                  Back
                </button>
                <button
                  type="button"
                  className="text-violet-700 hover:underline"
                  onClick={() => {
                    setCode("");
                    setStep(step === "code" ? "recovery" : "code");
                  }}
                >
                  {step === "code"
                    ? "Use a recovery code"
                    : "Use authenticator code"}
                </button>
              </div>
            )}
          </form>
        </div>
      </div>
    </div>
  );
}

Login.getLayout = function getLayout(page: React.ReactElement) {
  return page;
};
