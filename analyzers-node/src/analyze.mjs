// Analyzer de React/TypeScript. Es un subproceso: el backend .NET le pasa los archivos
// del repo por stdin (JSON) y este devuelve por stdout lo que detectó (componentes,
// imports locales y llamadas HTTP salientes). NO conoce los tipos del dominio (.NET
// construye los Node/Edge a partir de esta salida cruda).
//
// Análisis SINTÁCTICO (ts.createSourceFile), coherente con el analyzer de C#: razonamos
// sobre el árbol de sintaxis de cada archivo, sin type-checker ni resolución de módulos
// del compilador. Es una inferencia, por eso la confianza la fija .NET (< 100).
import ts from "typescript";
import path from "node:path";
import { fileURLToPath } from "node:url";

// Clasifica un archivo por convención de carpetas. Devuelve null si no es un componente
// que nos interese modelar (ej. un archivo de config suelto).
function classify(filePath) {
  if (/(^|\/)pages\//.test(filePath)) return "page";
  if (/(^|\/)components\//.test(filePath)) return "component";
  if (/(^|\/)hooks\//.test(filePath)) return "hook";
  if (/(^|\/)services\//.test(filePath)) return "service";
  return null;
}

// Nombre del componente a partir del archivo: "src/pages/ProductsPage.tsx" → "ProductsPage".
function baseName(filePath) {
  const file = filePath.split("/").pop() ?? filePath;
  return file.replace(/\.(tsx?|jsx?)$/, "");
}

function scriptKind(filePath) {
  if (filePath.endsWith(".tsx")) return ts.ScriptKind.TSX;
  if (filePath.endsWith(".jsx")) return ts.ScriptKind.JSX;
  if (filePath.endsWith(".ts")) return ts.ScriptKind.TS;
  return ts.ScriptKind.JS;
}

// Resuelve un import relativo ("../services/x") al archivo real dentro del set de entrada.
// Prueba las extensiones y el index/ como haría un bundler. Devuelve null si no es local.
function resolveRelativeImport(fromFile, specifier, fileSet) {
  const dir = path.posix.dirname(fromFile);
  const base = path.posix.normalize(path.posix.join(dir, specifier));
  const candidates = [
    base,
    `${base}.ts`,
    `${base}.tsx`,
    `${base}.js`,
    `${base}.jsx`,
    `${base}/index.ts`,
    `${base}/index.tsx`,
  ];
  return candidates.find((c) => fileSet.has(c)) ?? null;
}

// Extrae la URL de un argumento si es un literal de string (o template sin sustituciones).
function stringLiteralValue(argument) {
  if (!argument) return null;
  if (ts.isStringLiteral(argument) || ts.isNoSubstitutionTemplateLiteral(argument)) {
    return argument.text;
  }
  return null;
}

// Detecta una llamada HTTP saliente (fetch / axios). Devuelve { method, url } o null.
function extractApiCall(callExpression) {
  const callee = callExpression.expression;
  const firstArg = callExpression.arguments[0];

  // fetch("...")
  if (ts.isIdentifier(callee) && callee.text === "fetch") {
    const url = stringLiteralValue(firstArg);
    return url ? { method: "GET", url } : null;
  }

  // axios("...") — por defecto GET
  if (ts.isIdentifier(callee) && callee.text === "axios") {
    const url = stringLiteralValue(firstArg);
    return url ? { method: "GET", url } : null;
  }

  // axios.get("..."), axios.post("..."), etc.
  if (
    ts.isPropertyAccessExpression(callee) &&
    ts.isIdentifier(callee.expression) &&
    callee.expression.text === "axios"
  ) {
    const url = stringLiteralValue(firstArg);
    return url ? { method: callee.name.text.toUpperCase(), url } : null;
  }

  return null;
}

// Punto de entrada de la lógica (exportado para testear in-process sin lanzar el proceso).
export function analyze(files) {
  const fileSet = new Set(files.map((f) => f.path));
  const components = [];
  const imports = [];
  const apiCalls = [];

  for (const file of files) {
    const kind = classify(file.path);
    if (kind) {
      components.push({ name: baseName(file.path), kind, file: file.path });
    }

    const sourceFile = ts.createSourceFile(
      file.path,
      file.content,
      ts.ScriptTarget.Latest,
      /* setParentNodes */ true,
      scriptKind(file.path),
    );

    // Imports locales (relativos) que apuntan a otro archivo del repo.
    for (const statement of sourceFile.statements) {
      if (
        ts.isImportDeclaration(statement) &&
        statement.moduleSpecifier &&
        ts.isStringLiteral(statement.moduleSpecifier) &&
        statement.moduleSpecifier.text.startsWith(".")
      ) {
        const target = resolveRelativeImport(file.path, statement.moduleSpecifier.text, fileSet);
        if (target) {
          imports.push({ fromFile: file.path, toFile: target });
        }
      }
    }

    // Llamadas a API en cualquier parte del archivo (recorrido recursivo del AST).
    const visit = (node) => {
      if (ts.isCallExpression(node)) {
        const call = extractApiCall(node);
        if (call) {
          apiCalls.push({ fromFile: file.path, method: call.method, url: call.url });
        }
      }
      ts.forEachChild(node, visit);
    };
    visit(sourceFile);
  }

  return { components, imports, apiCalls };
}

// Lee todo el stdin y lo devuelve como string.
async function readStdin() {
  const chunks = [];
  for await (const chunk of process.stdin) {
    chunks.push(chunk);
  }
  return Buffer.concat(chunks).toString("utf8");
}

async function main() {
  const raw = await readStdin();
  const input = raw.trim() ? JSON.parse(raw) : { files: [] };
  const result = analyze(input.files ?? []);
  process.stdout.write(JSON.stringify(result));
}

// Ejecuta main() solo cuando se invoca directamente como `node src/analyze.mjs`
// (no cuando se importa desde un test).
const invokedAsScript =
  process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url);

if (invokedAsScript) {
  main().catch((error) => {
    process.stderr.write(String(error?.stack ?? error));
    process.exit(1);
  });
}
