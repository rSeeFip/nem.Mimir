import { readFileSync } from 'node:fs';
import path from 'node:path';

import { describe, expect, it } from 'vitest';

type PackageJson = {
  dependencies?: Record<string, string>;
  devDependencies?: Record<string, string>;
};

const getPackageJson = (): PackageJson => {
  const filePath = path.resolve(__dirname, '..', 'package.json');
  return JSON.parse(readFileSync(filePath, 'utf-8')) as PackageJson;
};

describe('dependency modernization guardrails', () => {
  it('uses modernized high-risk dependency baselines', () => {
    const pkg = getPackageJson();

    expect(pkg.dependencies?.next).toMatch(/^\^?(14|15)\./);
    expect(pkg.dependencies?.openai).toMatch(/^\^4\./);
    expect(pkg.dependencies?.['@tanstack/react-query']).toMatch(/^\^5\./);
    expect(pkg.dependencies?.['react-query']).toBeUndefined();
    expect(pkg.dependencies?.['eventsource-parser']).toMatch(/^\^?[1-9]\d*\./);
    expect(pkg.dependencies?.react).toBe('18.2.0');
    expect(pkg.dependencies?.['react-dom']).toBe('18.2.0');

    expect(pkg.devDependencies?.['@vitest/coverage-v8']).toBeDefined();
    expect(pkg.devDependencies?.['@vitest/coverage-c8']).toBeUndefined();
  });
});
