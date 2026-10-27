import path from 'node:path';
import { fileURLToPath } from 'node:url';

const sourceRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../src');
const identify = (filename) => path.relative(sourceRoot, filename).replaceAll('\\', '/').split('/');

export const architectureRule = {
  meta: {
    type: 'problem',
    schema: [],
    messages: {
      shared: 'Shared code cannot depend on app or features.',
      app: 'Features cannot depend on app; move reusable routing/configuration into shared.',
      feature: 'Cross-feature imports must use the target feature public index.',
    },
  },
  create(context) {
    function check(node, source) {
      if (typeof source !== 'string' || !source.startsWith('.')) return;
      const target = path.resolve(path.dirname(context.filename), source);
      const [layer, feature] = identify(context.filename);
      const [targetLayer, targetFeature, ...internalPath] = identify(target);
      if (layer === 'shared' && ['app', 'features'].includes(targetLayer)) {
        context.report({ node, messageId: 'shared' });
      } else if (layer === 'features' && targetLayer === 'app') {
        context.report({ node, messageId: 'app' });
      } else if (
        layer === 'features' &&
        targetLayer === 'features' &&
        feature !== targetFeature &&
        internalPath.length > 0 &&
        !/^index(?:\.[cm]?[jt]sx?)?$/.test(internalPath.join('/'))
      ) {
        context.report({ node, messageId: 'feature' });
      }
    }
    return {
      ImportDeclaration: (node) => check(node, node.source.value),
      ExportNamedDeclaration: (node) => {
        if (node.source) check(node, node.source.value);
      },
      ExportAllDeclaration: (node) => check(node, node.source.value),
      ImportExpression: (node) => check(node, node.source.value),
    };
  },
};
