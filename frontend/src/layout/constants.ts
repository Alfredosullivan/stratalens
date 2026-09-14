// Alto fijo del header de GraphPage, en px. Se define UNA vez aquí porque varios paneles
// laterales (GraphTreePanel, NodeDetailPanel) necesitan empezar justo debajo del header
// para no quedar tapados por él (o taparlo ellos) — antes cada panel adivinaba este
// número con su propio padding-top "mágico", y quedaba desincronizado si el header
// cambiaba de tamaño. Con una sola constante, cambiarla en un sitio lo corrige en todos.
export const HEADER_HEIGHT = 49;
