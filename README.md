# unity-interactive-grass
 
## Grass Model
Each grass model is considered a grass field that consists of multiple grass blades. 

## Grass blade
Each grass blade is controlled by their material. The physics compute shader (PCS) will calculate the position of the tip of the blade of the individual grass. PCS will take in an array of v2 positions, calculate physical model on the grass. It will output the positions.

Once the computation is finished. Hand off the results to the shader pipeline to render the grass.

## TODOs
[ ] - How do I get the compute shader value to the vertex shader material?