import { defineRouteMiddleware } from '@astrojs/starlight/route-data';

const base = import.meta.env.BASE_URL.replace(/\/$/, '');

// Starlight prefixes `base` on sidebar links but not on splash hero action links,
// so root-relative hero links are prefixed here instead of hardcoding it in content.
export const onRequest = defineRouteMiddleware((context) => {
  if (!base) return;

  const actions = context.locals.starlightRoute.entry.data.hero?.actions;
  if (!actions) return;

  for (const action of actions) {
    if (action.link.startsWith('/') && !action.link.startsWith(`${base}/`)) {
      action.link = `${base}${action.link}`;
    }
  }
});
