# C++ Dependency Visualizer

Left mouse + drag - move the graph

Mouse wheel - zoom graph on mouse cursor

Left mouse click on a header - display red connections to children (including) and green connections to parents (included by) of this header.

![Parents and children of header](neighbors.png)

Left mouse click on one header followed by shift + left mouse click on second header - display first found path between headers.

![Path between headers](path2.png)

Right mouse click on the canvas followed by choosing "Find All Cycles" from the context menu - display all cycles of headers.

![Cycles in headers](cycle1.png)

Layout and other options can be changed in menu.

![Different layouts](layouts.png)