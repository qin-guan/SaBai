import "@testing-library/jest-dom";

// @sabai/sdk configuration reads NEXT_PUBLIC_API_URL at import time via
// src/lib/envVariables.ts, which throws when the variable is missing. Provide a
// default so component tests can import the app modules.
process.env.NEXT_PUBLIC_API_URL ??= "http://localhost:5250";

// Workaround for vitest - mock HTMLDialogElement methods
// https://github.com/jsdom/jsdom/issues/3294
beforeAll(() => {
  // Server-side tests may run with `@vitest-environment node`, where window is undefined.
  if (typeof window === "undefined") return;

  HTMLDialogElement.prototype.show = vi.fn(function (this: HTMLDialogElement) {
    this.setAttribute("open", "");
  });
  HTMLDialogElement.prototype.showModal = vi.fn(function (
    this: HTMLDialogElement,
  ) {
    this.setAttribute("open", "");
  });
  HTMLDialogElement.prototype.close = vi.fn(function (this: HTMLDialogElement) {
    this.removeAttribute("open");
  });

  // Jsdom does not implement window.matchMedia by default, see below website for explanation:
  // https://rebeccamdeprey.com/blog/windowmatchmedia-in-vitest
  Object.defineProperty(window, "matchMedia", {
    writable: true,
    value: vi.fn().mockImplementation((query) => ({
      matches: false,
      media: query,
      onchange: null,
      addListener: vi.fn(), // deprecated
      removeListener: vi.fn(), // deprecated
      addEventListener: vi.fn(),
      removeEventListener: vi.fn(),
      dispatchEvent: vi.fn(),
    })),
  });
});
