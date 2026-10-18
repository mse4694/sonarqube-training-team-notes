// Fixture only; never executed and contains no actual token or secret.
function inspect(req) {
  // ruleid: training-auth-header-log
  console.log(req.headers.authorization);
  // ruleid: training-auth-header-log
  console.warn("debug", req.headers.authorization);
  // ok: training-auth-header-log
  console.log(req.headers["x-request-id"]);
  // ok: training-auth-header-log
  console.log("req.headers.authorization");
  const value = req.headers.authorization;
  // todoruleid: training-auth-header-log
  console.log(value); // known false negative: aliases require further analysis, not a safe claim.
}
