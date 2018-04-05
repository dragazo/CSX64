global open

# struct file
# qword fd
# qword buffer_pos
# qword buffer_len
# byte  buffer[buffer_cap]

# length of buffer array
def buffer_cap, 1024


# opens a new file handle from the specified path and mode
# must be closed via "close"
# ($1:64 char *path) ($2:64 mode) -> ($0:64 file*)
open:
    # allocate the file object
    push $1
    push $2
    mov $1, 8 * 3 + buffer_cap
    call [malloc]
    pop $2
    pop $1
    # ensure we got a proper allocation
    cmpz $0
    jnz [.aft]
    ret
    .aft:
    
    # open the file descriptor
    push $0
    mov $0, sys_open
    syscall
    
    # populate file object
    pop $1
    swap $0, $1
    mov [$0 + 0], $1
    mov [$0 + 8],  0
    mov [$0 + 16], 0
    
    # ensure file open worked
    not $1
    jz [.err]
    ret
    
    .err:
    mov $1, $0
    call [free]
    xor $0, $0
    ret

# closes a file opened via "open" and releases its resources
# ($1:64 file *file)
close: 
    # close the file
    push $1
    mov $0, sys_close
    mov $1, [$1 + 0]
    syscall
    
    # free the file object
    
    
    
    
    
    