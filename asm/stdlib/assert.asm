; source http://www.cplusplus.com/reference/cassert/

global assert

extern abort

segment .text

; void assert(int);
assert:
    cmp edi, 0
    jz .ret
    
    ; write error message
    mov eax, sys_write
    mov ebx, 2
    mov ecx, err_msg
    mov edx, err_msg_len
    syscall
    
    ; abort execution
    call abort
    
    .ret: ret

segment .rodata

err_msg: db "ASSERTION FAILURE", 10
err_msg_len: equ $-err_msg
