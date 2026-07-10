import { http, HttpResponse } from "msw";
import { buildClient, buildSalesPerson, buildSalesPersonFull, buildSalesOrgUnit } from "../fixtures";

const BASE = "/crmapi/api/v1";
const SALESORG_BASE = "/salesorgapi/api/v1";

export const handlers = [
  http.get(`${BASE}/clients`, () => HttpResponse.json([buildClient()])),
  http.get(`${SALESORG_BASE}/sales-people`, () => {
    const person = buildSalesPerson();
    const [firstName, ...rest] = person.displayName.split(" ");
    return HttpResponse.json([{ id: person.id, firstName, lastName: rest.join(" ") }]);
  }),
  http.get(`${SALESORG_BASE}/sales-org-units`, () => HttpResponse.json([buildSalesOrgUnit()])),
  // SalesHierarchyView also manages roles/assignments now (not just the read-only tree/lookup),
  // so its reload() fetches these too — empty lists are enough since no test asserts on them.
  http.get(`${SALESORG_BASE}/sales-roles`, () => HttpResponse.json([])),
  http.get(`${SALESORG_BASE}/sales-assignments`, () => HttpResponse.json([])),
  http.get(`${SALESORG_BASE}/sales-people/:id/manager-chain`, () =>
    HttpResponse.json([buildSalesPersonFull({ id: "manager-1", firstName: "Mona", lastName: "Manager" })])
  ),
  http.get(`${SALESORG_BASE}/sales-people/:id/reports`, () =>
    HttpResponse.json([buildSalesPersonFull({ id: "report-1", firstName: "Ricky", lastName: "Report" })])
  ),
];
