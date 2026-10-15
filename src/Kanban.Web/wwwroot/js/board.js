// Arrastrar y soltar tarjetas con SortableJS.
// SortableJS mueve nodos del DOM, pero el DOM es de Blazor: al soltar devolvemos el nodo
// a su sitio y avisamos a .NET, que actualiza el estado y vuelve a renderizar.

const boards = new WeakMap();

export function initBoard(root, dotnet) {
    disposeBoard(root);

    const sortables = [...root.querySelectorAll('.card-list')].map(list =>
        Sortable.create(list, {
            group: 'cards',
            animation: 150,
            ghostClass: 'card-ghost',
            dragClass: 'card-drag',
            // Eventos de puntero en vez del drag & drop nativo: mismo comportamiento con ratón y dedo,
            // y la tarjeta arrastrada se ve entera (en body, para que no la recorte el scroll de la columna).
            forceFallback: true,
            fallbackOnBody: true,
            // En táctil hay que mantener pulsado: así deslizar sigue haciendo scroll.
            delay: 180,
            delayOnTouchOnly: true,
            filter: '.move-menu',
            preventOnFilter: false,
            onEnd(evt) {
                const { item, from, to, oldIndex, newIndex } = evt;
                if (from === to && oldIndex === newIndex) return;

                item.remove();
                from.insertBefore(item, from.children[oldIndex] ?? null);

                dotnet.invokeMethodAsync('OnCardDropped', item.dataset.cardId, to.dataset.columnId, newIndex);
            },
        }));

    boards.set(root, sortables);
}

export function disposeBoard(root) {
    for (const sortable of boards.get(root) ?? []) sortable.destroy();
    boards.delete(root);
}
