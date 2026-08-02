# Inventory

Item definitions live in `Assets/Configs/Items.yaml`. Each item has a stable `id`,
display text, a root-relative sprite/texture path, quality, sell price, and max stack.

Starting contents live in `Assets/Configs/Inventory.yaml`. Every stack references an
item id and provides a quantity. Both configs are validated and hot-reloaded while
the game is running.

The inventory UI displays 16 stacks per page. Clicking a stack opens its detail
card; selling removes one item and credits its configured price to spirit stones.
Sorting orders by quality and then by name. Runtime mutations are currently
session-only; persistence can be layered onto `InventoryState` later.
