// Tests del detector de React/TS in-process: prueban la LÓGICA de detección sin lanzar
// el subproceso (importan analyze directamente). Corren con `npm test` (node --test)
// donde haya Node instalado. La invocación por Process.Start se prueba del lado .NET.
import { test } from "node:test";
import assert from "node:assert/strict";
import { analyze } from "../src/analyze.mjs";

test("detecta componentes por carpeta, imports locales y llamadas a API", () => {
  const files = [
    {
      path: "src/pages/ProductsPage.tsx",
      content: `
        import { getProducts } from "../services/productService";
        export function ProductsPage() {
          getProducts();
          return null;
        }
      `,
    },
    {
      path: "src/services/productService.ts",
      content: `
        import axios from "axios";
        export async function getProducts() {
          return axios.get("/api/products");
        }
      `,
    },
  ];

  const result = analyze(files);

  // Componentes clasificados por carpeta.
  assert.equal(result.components.length, 2);
  const page = result.components.find((c) => c.name === "ProductsPage");
  const service = result.components.find((c) => c.name === "productService");
  assert.equal(page.kind, "page");
  assert.equal(service.kind, "service");

  // Import local page → service resuelto al archivo real.
  assert.equal(result.imports.length, 1);
  assert.equal(result.imports[0].fromFile, "src/pages/ProductsPage.tsx");
  assert.equal(result.imports[0].toFile, "src/services/productService.ts");

  // Llamada a API detectada con método y URL.
  assert.equal(result.apiCalls.length, 1);
  assert.equal(result.apiCalls[0].method, "GET");
  assert.equal(result.apiCalls[0].url, "/api/products");
  assert.equal(result.apiCalls[0].fromFile, "src/services/productService.ts");
});

test("ignora imports de paquetes externos (no relativos)", () => {
  const files = [
    {
      path: "src/hooks/useAuth.ts",
      content: `import { useState } from "react"; export function useAuth(){ return useState(null); }`,
    },
  ];

  const result = analyze(files);

  assert.equal(result.components.length, 1);
  assert.equal(result.components[0].kind, "hook");
  assert.equal(result.imports.length, 0); // "react" no es local → no se modela
});

test("detecta fetch además de axios", () => {
  const files = [
    {
      path: "src/services/orderService.ts",
      content: `export async function createOrder(){ return fetch("/api/orders"); }`,
    },
  ];

  const result = analyze(files);

  assert.equal(result.apiCalls.length, 1);
  assert.equal(result.apiCalls[0].method, "GET");
  assert.equal(result.apiCalls[0].url, "/api/orders");
});
