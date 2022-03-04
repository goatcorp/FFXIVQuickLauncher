#version 330 core

uniform sampler2D FontTexture;

in vec4 color;
in vec2 texCoord;

out vec4 outputColor;

void main()
{
    outputColor = color * texture(FontTexture, texCoord);
}
