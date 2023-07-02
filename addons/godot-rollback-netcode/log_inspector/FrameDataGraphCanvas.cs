
using System;
using Godot;
using GDC = Godot.Collections;

[Tool]
public class FrameDataGraphCanvas : Control
{

    public const var Logger = GD.Load("res://addons/godot-rollback-netcode/Logger.gd");
    public const var LogData = GD.Load("res://addons/godot-rollback-netcode/log_inspector/LogData.gd");

    public int start_time = 0 {set{SetStartTime(value);
}}
	public int cursor_time = -1 { set{ SetCursorTime(value); } }

public bool show_network_arrows = true;
public GDC.Array network_arrow_peers = new GDC.Array() { };

public bool show_rollback_ticks = true;
public int max_rollback_ticks = 15;

public static readonly GDC.Dictionary FRAME_TYPE_COLOR = new GDC.Dictionary(){
        Logger.FrameType.INTERFRAME: new Color(0.3, 0.3, 0.3),
        Logger.FrameType.TICK: new Color(0.0, 0.75, 0.0),
        Logger.FrameType.INTERPOLATION_FRAME: new Color(0.0, 0.0, 0.5),
    };

public const Color ROLLBACK_LINE_COLOR := new Color(1.0, 0.5, 0.0)


    public const Color NETWORK_ARROW_COLOR1 := new Color(1.0, 0, 5, 1.0)

    public const Color NETWORK_ARROW_COLOR2 := new Color(0.0, 0.5, 1.0)

    public const int NETWORK_ARROW_SIZE := 8


    public const int EXTRA_WIDTH := 1000

    public const int PEER_GAP := 10

    public const int CURSOR_SCROLL_GAP := 100


    public LogData log_data

    public Font _font

    public Font _font_big

    [Signal] delegate void CursorTimeChanged(cursor_time);
[Signal] delegate void StartTimeChanged(start_time);

public void SetLogData(LogData _log_data)
{
    log_data = _log_data;

}

public void RefreshFromLogData()
{
    if (log_data.IsLoading())
    {
        return;

        // Remove any invalid peers from network_arrow_peers
    }
    foreach (var peer_id in network_arrow_peers)
    {
        if (!peer_id in log_data.peer_ids)
			{
    network_arrow_peers.Erase(peer_id);

}
		}
		if (show_network_arrows)
{
    // If we have at least two peers, set network_arrow_peers to first valid
    // options.
    if (network_arrow_peers.Size() < 2 && log_data.peer_ids.Size() >= 2)
    {
        network_arrow_peers = [log_data.peer_ids[0], log_data.peer_ids[1]]


            }
}
Update();
	
	}
	
	public void SetStartTime(int _start_time)
{
    if (start_time != _start_time)
    {
        start_time = _start_time;
        Update();
        EmitSignal("start_time_changed", start_time);

    }
}

public void SetCursorTime(int _cursor_time)
{
    if (cursor_time != _cursor_time)
    {
        cursor_time = _cursor_time;
        Update();
        EmitSignal("cursor_time_changed", cursor_time);

        var relative_cursor_time = cursor_time - start_time;
        if (relative_cursor_time < 0)
        {
            SetStartTime(cursor_time - (rect_size.x - CURSOR_SCROLL_GAP));
        }
        else if (relative_cursor_time > rect_size.x)
        {
            SetStartTime(cursor_time - CURSOR_SCROLL_GAP);

        }
    }
}

public void _Ready()
{
    _font = new DynamicFont()

        _font.font_data = GD.Load("res://addons/godot-rollback-netcode/log_inspector/monogram_extended.ttf");
    _font.size = 16;

    _font_big = new DynamicFont()

        _font_big.font_data = GD.Load("res://addons/godot-rollback-netcode/log_inspector/monogram_extended.ttf");
    _font_big.size = 32;

}

public void _GuiInput(InputEvent event)
{
    if (event is InputEventMouseButton)
		{
    if (event.button_index == BUTTON_LEFT && event.pressed)
			{
        SetCursorTime((int)(start_time + event.position.x));

    }
}
	}
	
	public void _DrawPeer(int peer_id, Rect2 peer_rect, GDC.Dictionary draw_data)
{
    var relative_start_time = start_time - EXTRA_WIDTH;
    if (relative_start_time < 0)
    {
        relative_start_time = 0;

    }
    int absolute_start_time = log_data.start_time + relative_start_time;
    int absolute_end_time = absolute_start_time + peer_rect.size.x + (EXTRA_WIDTH * 2);
    LogData frame.FrameData = log_data.GetFrameByTime(peer_id, absolute_start_time);
    if (frame == null && log_data.GetFrameCount(peer_id) > 0)
    {
        frame = log_data.GetFrame(peer_id, 0);
    }
    if (frame == null)
    {
        return;

    }
    GDC.Array tick_numbers_to_draw = new GDC.Array() { };

    bool capture_network_arrow_positions = show_network_arrows && peer_id in network_arrow_peers;
    GDC.Dictionary network_arrow_start_positions = new GDC.Dictionary() { };
    GDC.Dictionary network_arrow_end_positions = new GDC.Dictionary() { };

    int other_network_arrow_peer_id

        if (capture_network_arrow_positions)
    {
        foreach (var other_peer_id in network_arrow_peers)
        {
            if (other_peer_id != peer_id)
            {
                other_network_arrow_peer_id = other_peer_id;
                break;
            }
        }
    }
    string other_network_arrow_peer_key = "remote_ticks_received_from_%s" % other_network_arrow_peer_id;

    // Adjust the peer rect for the extra width.
    var extended_peer_rect = peer_rect;
    extended_peer_rect.position.x -= (start_time > EXTRA_WIDTH ? EXTRA_WIDTH : start_time)

        extended_peer_rect.size.x += (EXTRA_WIDTH * 2);

    var last_rollback_point = null;

    while (frame.start_time <= absolute_end_time)
    {
        Rect2 frame_rect = new Rect2(
            new Vector2(extended_peer_rect.position.x + frame.start_time - absolute_start_time, extended_peer_rect.position.y),
            new Vector2(frame.end_time - frame.start_time, extended_peer_rect.size.y));
        if (frame_rect.Intersects(extended_peer_rect))
        {
            frame_rect = frame_rect.Clip(extended_peer_rect);
            if (frame_rect.size.x == 0)
            {
                frame_rect.size.x = 1;

            }
            bool skipped = frame.data.Get("skipped", false);
            bool fatal_error = frame.data.Get("fatal_error", false);
            Vector2 center_position = frame_rect.position + (frame_rect.size / 2.0);
            Color frame_color


                if (fatal_error)
            {
                frame_color = new Color(1.0, 0.0, 0.0);
            }
            else if (skipped)
            {
                frame_color = new Color(1.0, 1.0, 0.0);
                if (frame_rect.size.x <= 1.0)
                {
                    frame_rect.size.x = 3;
                    frame_rect.position.x -= 1.5;

                }
                if (frame.data.Contains("skip_reason"))
                {
                    String tick_letter = "";

                        case Match(int)(frame.data["skip_reason"]):

                            case Logger.SkipReason.INPUT_BUFFER_UNDERRUN:
    tick_letter = "B";
    break;
case Logger.SkipReason.WAITING_TO_REGAIN_SYNC:
    tick_letter = "W";
    break;
case Logger.SkipReason.ADVANTAGE_ADJUSTMENT:
    tick_letter = "A";
    break;
    break;
    if (tick_letter != "")
    {
        tick_numbers_to_draw.Append(new GDC.Array() { _font_big, center_position - new Vector2(5, 0), tick_letter, new Color("f04dff") });
    }
}
				}

                else
{
    frame_color = FRAME_TYPE_COLOR[frame.type];

}
DrawRect(frame_rect, frame_color);

if (frame.type == Logger.FrameType.TICK && frame.data.Contains("tick") && !skipped)
{
    int tick = frame.data["tick"];
    tick_numbers_to_draw.Append(new GDC.Array() { _font, center_position - new Vector2(3, 0), GD.Str(tick), new Color(1.0, 1.0, 1.0) });
    if (frame.data.Contains("input_tick") && capture_network_arrow_positions)
    {
        int input_tick = frame.data["input_tick"];
        network_arrow_start_positions[input_tick] = center_position;

    }
}
if (capture_network_arrow_positions && frame.data.Contains(other_network_arrow_peer_key))
{
    foreach (var tick in frame.data[other_network_arrow_peer_key])
    {
        network_arrow_end_positions[(int)(tick)] = center_position;

    }
}
if (show_rollback_ticks && frame.data.Contains("rollback_ticks"))
{
    var rollback_height = extended_peer_rect.size.y * ((float)(frame.data["rollback_ticks"]) / (float)(max_rollback_ticks));
    if (rollback_height > extended_peer_rect.size.y)
    {
        rollback_height = extended_peer_rect.size.y;
    }
    Vector2 rollback_point = new Vector2(center_position.x, frame_rect.position.y + frame_rect.size.y - rollback_height);
    if (last_rollback_point != null)
    {
        DrawLine(last_rollback_point, rollback_point, ROLLBACK_LINE_COLOR, 2.0, true);
    }
    last_rollback_point = rollback_point;

    // Move on to the next frame.
}
			}
			if (frame.frame < log_data.GetFrameCount(peer_id) - 1)
{
    frame = log_data.GetFrame(peer_id, frame.frame + 1);
}
else
{
    break;

}
		}
		foreach (var tick_number_to_draw in tick_numbers_to_draw)
{
    DrawString(tick_number_to_draw[0], tick_number_to_draw[1], tick_number_to_draw[2], tick_number_to_draw[3]);

}
if (capture_network_arrow_positions)
{
    if (!draw_data.Contains("network_arrow_positions"))
    {
        draw_data["network_arrow_positions"] = new GDC.Array() { };
    }
    draw_data["network_arrow_positions"].Append(new GDC.Array() { network_arrow_start_positions, network_arrow_end_positions });

}
	}
	
	public void _DrawNetworkArrows(GDC.Dictionary start_positions, GDC.Dictionary end_positions, Color color)
{
    foreach (var tick in start_positions)
    {
        if (!end_positions.Contains(tick))
        {
            continue;
        }
        var start_position = start_positions[tick];
        var end_position = end_positions[tick];

        if (start_position.y < end_position.y)
        {
            start_position.y += 10;
            end_position.y -= 15;
        }
        else
        {
            start_position.y -= 15;
            end_position.y += 10;

        }
        DrawLine(start_position, end_position, color, 2.0, true);

        // Draw the arrow head.
        var sqrt12 = Mathf.Sqrt(0.5);
        Vector2 vector = end_position - start_position;
        Transform2D t = new Transform2D(vector.Angle(), end_position);
        var points = PoolVector2Array(new GDC.Array(){
                t.Xform(new Vector2(0, 0)),
                t.Xform(new Vector2(-NETWORK_ARROW_SIZE, sqrt12 * NETWORK_ARROW_SIZE)),
                t.Xform(new Vector2(-NETWORK_ARROW_SIZE, sqrt12 * -NETWORK_ARROW_SIZE)),
            });
        var colors = PoolColorArray(new GDC.Array(){
                color,
                color,
                color,
            });
        DrawPrimitive(points, colors, PoolVector2Array());

    }
}

public void _Draw()
{
    if (log_data == null || log_data.IsLoading())
    {
        return;
    }
    var peer_count = log_data.peer_ids.Size();
    if (peer_count == 0)
    {
        return;

    }
    GDC.Dictionary draw_data = new GDC.Dictionary() { };
    GDC.Dictionary peer_rects = new GDC.Dictionary() { };

    float peer_height = (rect_size.y - ((peer_count - 1) * PEER_GAP)) / peer_count;
    int current_y = 0;
    foreach (var peer_index in GD.Range(peer_count))
    {
        var peer_id = log_data.peer_ids[peer_index];
        Rect2 peer_rect = new Rect2(
            new Vector2(0, current_y),
            new Vector2(rect_size.x, peer_height));
        peer_rects[peer_id] = peer_rect;
        _DrawPeer(peer_id, peer_rect, draw_data);
        current_y += (peer_height + PEER_GAP);

    }
    if (show_network_arrows)
    {
        GDC.Array network_arrows_positions = draw_data.Get("network_arrow_positions", new GDC.Array() { });
        if (network_arrows_positions.Size() == 2)
        {
            _DrawNetworkArrows(network_arrows_positions[0][0], network_arrows_positions[1][1], NETWORK_ARROW_COLOR1);
            _DrawNetworkArrows(network_arrows_positions[1][0], network_arrows_positions[0][1], NETWORK_ARROW_COLOR2);

        }
    }
    foreach (var peer_id in peer_rects)
    {
        Rect2 peer_rect = peer_rects[peer_id];
        DrawString(_font, peer_rect.position + new Vector2(0, PEER_GAP), "Peer %s" % peer_id, new Color(1.0, 1.0, 1.0));

    }
    if (cursor_time >= start_time && cursor_time <= start_time + rect_size.x)
    {
        DrawLine(
            new Vector2(cursor_time - start_time, 0),
            new Vector2(cursor_time - start_time, rect_size.y),
            new Color(1.0, 0.0, 0.0),
            3.0);


    }
}
	
	
	
}