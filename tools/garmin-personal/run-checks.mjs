import { runChecks, runSdkChecks } from "./checks.mjs";
console.log(await runChecks());
console.log(await runSdkChecks(await import("@garmin/fitsdk")));
