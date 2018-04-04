global main

def stdin,  0
def stdout, 1

main:
    mov $0, sys_write
    mov $1, stdout
    mov $2, msg
    mov $3, msg_len

    mov $10, 30
    .top:
    syscall
    loop $10, [.top]
    
    ret
    

msg: emit:16 "this is a really long line that is going to go way far off the screen and junk", 10
.e: def msg_len, .e-msg