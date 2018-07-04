; source http://www.cplusplus.com/reference/cassert/

global assert

segment .text

; void assert(int);
assert:
    cmp edi, 0
    jz .ret
    
    mov eax, sys_exit
    mov ebx, 666 ; evil error code 666 is assertion failure
    syscall
    
    .ret: ret
    