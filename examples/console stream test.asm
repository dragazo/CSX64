global main

def stdin,  0
def stdout, 1

main:
    mov $0, sys_write
    mov $1, stdout
    mov $2, q
    mov $3, q_len
    syscall
    
    mov $0, sys_read
    mov $1, stdin
    mov $2, name
    mov $3, name_len
    syscall
    
    ; store length of resulting name in $10
    mov $10, $0
    sub $10, 4 ; get rid of the new line char (-4 because UTF-16 \r\n)
    
    mov $0, sys_write
    mov $1, stdout
    mov $2, a
    mov $3, a_len
    syscall
    
    mov $2, name
    mov $3, $10
    syscall
    
    mov $2, b
    mov $3, b_len
    syscall
    
    ret

q: emit:16 "what's your name? "
.e: def q_len, .e-q

name: emit:16 #32
.e: def name_len, .e-name

a: emit:16 "so your name is "
.e: def a_len, .e-a
b: emit:16 '?', 10, "what a cool name!", 10
.e: def b_len, .e-b