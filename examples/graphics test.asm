global main

main:
    call [render]
    slp 10000
    jmp [main]

update_cursor_pos:
    mov $0, sys_getmousepos
    syscall
    
    ; center on x
    mov:32 $0, [rect_w]
    sr:32 $0, 1
    sub:32 $1, $0
    
    ; center on y
    mov:32 $0, [rect_h]
    sr:32 $0, 1
    sub:32 $2, $0
    
    mov:32 [rect_x], $1
    mov:32 [rect_y], $2
    
    ret
    
render:
    ; clear the screen
    mov $0, sys_clear
    mov:32 $1, [back_color]
    syscall
    
    ; update cursor pos
    call [update_cursor_pos]
    
    ; get proper brush for rendering cursor
    mov $0, sys_getmousedown
    syscall
    movz $1, mouse_brush
    movnz $1, mouse_down_brush
    mov $0, sys_setbrush
    syscall
    
    ; draw cursor
    mov $0, sys_fillellipse
    mov $1, rect
    syscall
    
    ; render the frame
    mov $0, sys_render
    syscall
    
    ret

; -----------------------------------

back_color: emit:32 0xff7da3e0
mouse_brush:
    mouse_brush_type: emit:8  0
    mouse_brush_fore: emit:32 0xffd1942b
    mouse_brush_back: emit:32 0
mouse_down_brush:
    mouse_down_brush_type: emit:8  0
    mouse_down_brush_fore: emit:32 0xffc94220
    mouse_down_brush_back: emit:32 0
rect:
    rect_x: emit:32 0
    rect_y: emit:32 0
    rect_w: emit:32 30
    rect_h: emit:32 30
    












