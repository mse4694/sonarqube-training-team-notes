'use client';

import { useEffect } from 'react';

// Discussion point (Day 1): the session cookie is not HttpOnly on `main`, so client JS can read
// it and copy it into localStorage - exactly what an XSS payload would do to exfiltrate it.
// On `solution` the cookie is HttpOnly, so document.cookie never exposes it and this finds nothing.
export function TokenStash() {
  useEffect(() => {
    const match = document.cookie.match(/(?:^|;\s*)session=([^;]+)/);
    if (match) {
      localStorage.setItem('tn_token', match[1]);
    }
  }, []);
  return null;
}
