import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { test } from 'node:test';
import { RuleTester } from 'eslint';
import { architectureRule } from './architecture.mjs';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../src');
const filename = (relative) => path.join(root, relative);

test('architecture boundaries allow composition and public APIs, reject reverse and deep imports', () => {
  new RuleTester({ languageOptions: { ecmaVersion: 'latest', sourceType: 'module' } }).run(
    'architecture/boundaries',
    architectureRule,
    {
      valid: [
        {
          filename: filename('app/App.tsx'),
          code: "import { Page } from '../features/shipments/ShipmentListPage';",
        },
        {
          filename: filename('features/dashboard/Page.tsx'),
          code: "import { useShipments } from '../shipments';",
        },
        { filename: filename('features/shipments/Page.tsx'), code: "import { get } from './api';" },
        {
          filename: filename('shared/auth/Session.ts'),
          code: "import { routePaths } from '../routing/routePaths';",
        },
        {
          filename: filename('features/dashboard/Page.tsx'),
          code: "export { useShipments } from '../shipments/index';",
        },
      ],
      invalid: [
        {
          filename: filename('shared/auth/Session.ts'),
          code: "import { Page } from '../../app/App';",
          errors: [{ messageId: 'shared' }],
        },
        {
          filename: filename('shared/lib/helper.ts'),
          code: "export * from '../../features/shipments/api';",
          errors: [{ messageId: 'shared' }],
        },
        {
          filename: filename('features/dashboard/Page.tsx'),
          code: "import { routes } from '../../app/router/routes';",
          errors: [{ messageId: 'app' }],
        },
        {
          filename: filename('features/dashboard/Page.tsx'),
          code: "import { get } from '../shipments/api';",
          errors: [{ messageId: 'feature' }],
        },
        {
          filename: filename('features/dashboard/Page.tsx'),
          code: "const data = import('../shipments/queries');",
          errors: [{ messageId: 'feature' }],
        },
      ],
    },
  );
});
