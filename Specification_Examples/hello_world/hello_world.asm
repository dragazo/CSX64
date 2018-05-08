global main ; the linker requires a global named main

main: ; a label marking the beginning of main function
    mov $0, sys_write ; load a system call code into $0
    mov $1, 1         ; load file descriptor into $1 (fd 1 is stdout)
    mov $2, txt       ; move the value of txt (an address) into $2
    mov $3, txt_len   ; move the length of txt into $3
    syscall           ; perform the system call
    
    xor $0, $0        ; zero out $0 (using the assembly xor idiom)
    ret               ; return 0 to caller (in this case the OS)

; create a label for txt and emit a string and a new line character
txt: emit:8 "Hello World!", 10
; define a new symbol txt_len to be the length of the string
def txt_len, @-txt ; (uses @ current line macro)
