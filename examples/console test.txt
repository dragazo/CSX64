global main

main:
    mov $0, sys_puts
    mov $1, str
    
    mov:16 [str], 'A'
    jmp [.aft]
    .top:
        syscall
    .mid: inc:16 [str]
    .aft:
        cmp:16 [str], 'z'
        jbe [.top]
    
    #print ending message
    mov $1, msg
    syscall
    
    ret

str: emit:16 0, 10, 0
msg: emit:16 10, "all done", 10, ":D", 0