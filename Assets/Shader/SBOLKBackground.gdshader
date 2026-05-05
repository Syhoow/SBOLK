shader_type canvas_item;

uniform float screen_height = 270.0;

void fragment() {

	float diff_x = 0.0;

	if ( int(UV.y * screen_height) % 2 == 0 ){

		diff_x += 0.075 * sin((10.0 * UV.y) + (2.0 * TIME));

	}
	else{

		diff_x += -0.075 * sin((10.0 * UV.y) + (2.0 * TIME));

	}
	
	COLOR = texture(TEXTURE, vec2(UV.x + diff_x, UV.y));
}