# Frontend migration — toolchain pins Phase 0

Ngày chốt: 25/10/2026. Đây là exact pins để T01.01 tạo package.json/lockfile; chưa có frontend project và chưa cài dependency vào sản phẩm. [Contract/status](react-migration-contracts.md) và [plan](../../refactorfe.md) xác định phạm vi.

## Runtime đích và môi trường hiện tại

| Tool | Pin đích | Hiện có local |
| --- | --- | --- |
| Node.js LTS | 24.22.1 (Krypton, release 20/10/2026) | 22.18.0 — cần nâng trước T01.01 |
| npm | 11.19.1 (bundled với Node target) | 10.9.3 — cần nâng theo Node target |
| .NET SDK | Giữ net10.0 / toolchain backend hiện tại | 10.0.202 đã chạy baseline |

Node release được đối chiếu từ [index chính thức](https://nodejs.org/dist/index.json) và [lịch hỗ trợ Node](https://nodejs.org/en/about/previous-releases). Nâng Node/npm là prerequisite môi trường của Phase 1, không cài hoặc đổi runtime máy trong Phase 0.

## Exact package versions

| Package | Pin | Nhóm | Nguồn metadata chính thức |
| --- | --- | --- | --- |
| `react` | `19.3.1` | runtime | [registry metadata](https://registry.npmjs.org/react/19.3.1) |
| `react-dom` | `19.3.1` | runtime | [registry metadata](https://registry.npmjs.org/react-dom/19.3.1) |
| `antd` | `6.6.7` | runtime | [registry metadata](https://registry.npmjs.org/antd/6.6.7) |
| `@ant-design/icons` | `6.3.4` | runtime | [registry metadata](https://registry.npmjs.org/@ant-design%2ficons/6.3.4) |
| `dayjs` | `1.11.23` | runtime | [registry metadata](https://registry.npmjs.org/dayjs/1.11.23) |
| `vite` | `8.3.5` | dev | [registry metadata](https://registry.npmjs.org/vite/8.3.5) |
| `@vitejs/plugin-react` | `6.1.2` | dev | [registry metadata](https://registry.npmjs.org/@vitejs%2fplugin-react/6.1.2) |
| `typescript` | `5.9.3` | dev | [registry metadata](https://registry.npmjs.org/typescript/5.9.3) |
| `react-router` | `8.5.0` | runtime | [registry metadata](https://registry.npmjs.org/react-router/8.5.0) |
| `@tanstack/react-query` | `5.104.5` | runtime | [registry metadata](https://registry.npmjs.org/@tanstack%2freact-query/5.104.5) |
| `vitest` | `5.0.5` | dev | [registry metadata](https://registry.npmjs.org/vitest/5.0.5) |
| `jsdom` | `30.1.3` | dev | [registry metadata](https://registry.npmjs.org/jsdom/30.1.3) |
| `@testing-library/react` | `16.3.3` | dev | [registry metadata](https://registry.npmjs.org/@testing-library%2freact/16.3.3) |
| `@testing-library/dom` | `10.4.2` | dev | [registry metadata](https://registry.npmjs.org/@testing-library%2fdom/10.4.2) |
| `@testing-library/user-event` | `14.6.8` | dev | [registry metadata](https://registry.npmjs.org/@testing-library%2fuser-event/14.6.8) |
| `@testing-library/jest-dom` | `7.0.1` | dev | [registry metadata](https://registry.npmjs.org/@testing-library%2fjest-dom/7.0.1) |
| `@playwright/test` | `1.64.1` | dev | [registry metadata](https://registry.npmjs.org/@playwright%2ftest/1.64.1) |
| `openapi-typescript` | `7.13.0` | dev | [registry metadata](https://registry.npmjs.org/openapi-typescript/7.13.0) |
| `eslint` | `10.13.0` | dev | [registry metadata](https://registry.npmjs.org/eslint/10.13.0) |
| `@eslint/js` | `10.0.1` | dev | [registry metadata](https://registry.npmjs.org/@eslint%2fjs/10.0.1) |
| `typescript-eslint` | `8.72.0` | dev | [registry metadata](https://registry.npmjs.org/typescript-eslint/8.72.0) |
| `eslint-plugin-react-hooks` | `7.1.1` | dev | [registry metadata](https://registry.npmjs.org/eslint-plugin-react-hooks/7.1.1) |
| `eslint-plugin-react-refresh` | `0.5.7` | dev | [registry metadata](https://registry.npmjs.org/eslint-plugin-react-refresh/0.5.7) |
| `globals` | `17.13.0` | dev | [registry metadata](https://registry.npmjs.org/globals/17.13.0) |
| `@types/react` | `19.3.0` | dev | [registry metadata](https://registry.npmjs.org/@types%2freact/19.3.0) |
| `@types/react-dom` | `19.3.0` | dev | [registry metadata](https://registry.npmjs.org/@types%2freact-dom/19.3.0) |
| `@types/node` | `24.19.2` | dev | [registry metadata](https://registry.npmjs.org/@types%2fnode/24.19.2) |

## Compatibility đã kiểm tra

- Đọc engines/peerDependencies/peerDependenciesMeta từ registry chính thức cho cả 27 pins; kiểm tra 51 engine/peer constraints bằng semver của npm đã có: 0 conflict/missing required peer. Không cài optional CSS/compiler/browser packages chỉ vì có optional peer.
- React và ReactDOM cùng 19.3.1; AntD 6 yêu cầu React>=18. React Router 8.5.0 yêu cầu Node>=22.22.0, React>=19.2.7; jsdom 30.1.3 yêu cầu Node^22.22.2 hoặc ^24.15.0 hoặc >=26. Node 24.22.1 thỏa, Node local 22.18.0 không thỏa.
- Không dùng TypeScript latest 7.0.2: openapi-typescript 7.13.0 khai báo peer TypeScript^5.x, typescript-eslint 8.72.0 yêu cầu <6.1.0. Pin TypeScript 5.9.3 thỏa cả hai. React type packages cùng 19.3.0; @types/node thuộc major 24 phù hợp Node target.
- Vite 8.3.5 + plugin-react 6.1.2 + Vitest 5.0.5 có peer range tương thích; openapi-typescript dùng cho schema OpenAPI 3.0/3.1, không buộc thay partner document.
- Chạy `npm install --package-lock-only --ignore-scripts --strict-peer-deps --dry-run --no-audit --no-fund` trên candidate manifest trong thư mục scratch bị Git ignore: exit 0, không ERESOLVE, không tạo node_modules. Có 35 EBADENGINE warning do resolver chạy bằng Node/npm local cũ; không coi dry-run này là runtime build pass. Target engine/peer constraints được kiểm tra riêng theo exact pins ở trên.
- Kiểm tra metadata + dry-run không phải audit bảo mật, lockfile đã cài hoặc test render/browser. T01.01 dùng đúng Node/npm rồi install thật, npm ci/typecheck/lint/test/build; nếu dependency graph đã thay đổi cần ghi decision, không đổi pins thầm lặng.

## Quy tắc scaffold/reproducibility

- .node-version ghi 24.22.1; packageManager npm@11.19.1 và engines theo Node/npm target; .npmrc dùng save-exact=true. Commit package-lock.json sinh bằng npm target, không commit candidate manifest/log/node_modules từ scratch.
- package.json dùng version cụ thể ở bảng, không caret/tilde/latest cho direct dependencies. Lockfile chốt transitive resolution. Dùng template có kiểm soát và thay pins rõ ràng; không chạy create-vite@latest rồi chấp nhận mặc định TypeScript khác bảng.
- Không thêm react-router-dom chỉ vì template cũ; pin react-router ở chế độ declarative của plan. Không thêm Axios, Redux/Zustand, form/schema library thứ hai, Ant Design Pro hoặc chart library khi chưa có nhu cầu task.
- ESLint flat config theo ESLint 10/typescript-eslint 8; Vite plugin React 6 và TypeScript 5.9 strict. Không dựa config mặc định của phiên bản template không được pin.
- Frontend CI dùng cùng Node/npm/lockfile. Chưa thêm lệnh npm vào supported commands của repo cho đến khi scripts tồn tại và đã chạy được.

## Nguồn hướng dẫn

[Ant Design với Vite](https://ant.design/docs/react/use-with-vite/), [Vite guide](https://vite.dev/guide/), [Vitest guide](https://vitest.dev/guide/), [React Router](https://reactrouter.com/start/declarative/installation) và [openapi-typescript](https://openapi-ts.dev/introduction). Version pins lấy registry theo từng version ở bảng; không suy ra compatibility từ trang docs đang dùng latest.
