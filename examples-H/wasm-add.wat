(module
  (func (export "run") (param i32) (param i32) (result i32) (local i32) (local i32) (local i32) (local i32)
    i32.const 0
    local.set 2
    loop
      local.get 2
      i32.const 0
      i32.eq
      if
        local.get 0
        local.set 3
        local.get 1
        local.set 4
        local.get 3
        local.get 4
        i32.add
        local.set 5
        local.get 5
        return
      end
      unreachable
    end
    unreachable
  )
)
