# Bounce #

## Overview ##
This sample demonstrates collision detection based on a Binary Space Partitioning tree. It makes use of the [XNA Collidable Model](http://code.google.com/p/xna-collidable-model/) content pipeline extension to efficiently find the collisions of a user controled amount of spheres with the base arena geometry. Spheres are controlled via a simple physics engine so they correctly bounce off walls and each other. Hardware instancing speeds up the rendering of hundreds of spheres.

## Features ##
  * Direct sphere-mesh collision detection using BSP tree
  * Simple Rigid body physics engine
  * Hardware instancing
  * Normal mapping

## Controls ##
  * 'A', 'S', 'D', 'W' and 'Space' translate the camera
  * Mouse movement rotates the camera
  * Left mouse click releases a new sphere
  * 'N' toggles normal mapping, 'T' key toggles textures
  * 'C' toggles display of the colliding triangles
  * 'G' shows the collision geometry
  * 'L' toggles light position


## News ##
2012/05/24: I created a [branch](http://code.google.com/p/xna-bounce/source/browse/#svn%2Fbranches%2Fshadows) for playing around with shadow maps

## Acknowledgements and References ##
Special thanks to Roni Oeschger for creating the initial version of the physics engine and Thomas Oskam for the Arena and Sphere models!

You can find some more info on my [homepage](http://www.theomader.com/public/bounce.html)

Let me know what you think!!