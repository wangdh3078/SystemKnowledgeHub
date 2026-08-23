export type LineDiffKind = 'unchanged' | 'removed' | 'added'

export interface LineDiffSegment {
  readonly kind: LineDiffKind
  readonly lines: readonly string[]
}

interface LineEdit {
  readonly kind: LineDiffKind
  readonly line: string
}

/**
 * Empty content has no lines. A non-empty trailing LF creates a final empty
 * line token, so adding/removing the final newline is represented explicitly.
 */
export function splitBodyLines(value: string): readonly string[] {
  return value.length === 0 ? [] : value.split('\n')
}

export function myersLineDiff(
  fromLines: readonly string[],
  toLines: readonly string[],
): readonly LineDiffSegment[] {
  return compact(diffRange(fromLines, 0, fromLines.length, toLines, 0, toLines.length))
}

function diffRange(
  fromLines: readonly string[],
  fromStart: number,
  fromEnd: number,
  toLines: readonly string[],
  toStart: number,
  toEnd: number,
): LineEdit[] {
  const edits: LineEdit[] = []
  let prefixLength = 0
  while (
    fromStart + prefixLength < fromEnd
    && toStart + prefixLength < toEnd
    && fromLines[fromStart + prefixLength] === toLines[toStart + prefixLength]
  ) {
    edits.push({ kind: 'unchanged', line: fromLines[fromStart + prefixLength] })
    prefixLength += 1
  }

  fromStart += prefixLength
  toStart += prefixLength
  let suffixLength = 0
  while (
    fromStart < fromEnd - suffixLength
    && toStart < toEnd - suffixLength
    && fromLines[fromEnd - suffixLength - 1] === toLines[toEnd - suffixLength - 1]
  ) {
    suffixLength += 1
  }

  const fromCoreEnd = fromEnd - suffixLength
  const toCoreEnd = toEnd - suffixLength
  if (fromStart === fromCoreEnd) {
    for (let index = toStart; index < toCoreEnd; index += 1) {
      edits.push({ kind: 'added', line: toLines[index] })
    }
  } else if (toStart === toCoreEnd) {
    for (let index = fromStart; index < fromCoreEnd; index += 1) {
      edits.push({ kind: 'removed', line: fromLines[index] })
    }
  } else {
    edits.push(
      ...bisect(
        fromLines,
        fromStart,
        fromCoreEnd,
        toLines,
        toStart,
        toCoreEnd,
      ),
    )
  }

  for (let offset = suffixLength; offset > 0; offset -= 1) {
    edits.push({ kind: 'unchanged', line: fromLines[fromEnd - offset] })
  }
  return edits
}

/** Linear-space Myers bisect with stable delete-before-insert tie breaking. */
function bisect(
  fromLines: readonly string[],
  fromStart: number,
  fromEnd: number,
  toLines: readonly string[],
  toStart: number,
  toEnd: number,
): LineEdit[] {
  const fromLength = fromEnd - fromStart
  const toLength = toEnd - toStart
  const maximumDistance = Math.ceil((fromLength + toLength) / 2)
  const offset = maximumDistance
  const vectorLength = maximumDistance * 2 + 2
  const forward = new Int32Array(vectorLength)
  const reverse = new Int32Array(vectorLength)
  forward.fill(-1)
  reverse.fill(-1)
  forward[offset + 1] = 0
  reverse[offset + 1] = 0

  const delta = fromLength - toLength
  const frontOverlap = delta % 2 !== 0
  let forwardStart = 0
  let forwardEnd = 0
  let reverseStart = 0
  let reverseEnd = 0

  for (let distance = 0; distance <= maximumDistance; distance += 1) {
    for (
      let diagonal = -distance + forwardStart;
      diagonal <= distance - forwardEnd;
      diagonal += 2
    ) {
      const vectorIndex = offset + diagonal
      let fromIndex = diagonal === -distance
        || (diagonal !== distance && forward[vectorIndex - 1] < forward[vectorIndex + 1])
        ? forward[vectorIndex + 1]
        : forward[vectorIndex - 1] + 1
      let toIndex = fromIndex - diagonal
      while (
        fromIndex < fromLength
        && toIndex < toLength
        && fromLines[fromStart + fromIndex] === toLines[toStart + toIndex]
      ) {
        fromIndex += 1
        toIndex += 1
      }
      forward[vectorIndex] = fromIndex

      if (fromIndex > fromLength) forwardEnd += 2
      else if (toIndex > toLength) forwardStart += 2
      else if (frontOverlap) {
        const reverseIndex = offset + delta - diagonal
        if (reverseIndex >= 0 && reverseIndex < vectorLength && reverse[reverseIndex] !== -1) {
          const reverseFromIndex = fromLength - reverse[reverseIndex]
          if (fromIndex >= reverseFromIndex) {
            return splitAt(
              fromLines,
              fromStart,
              fromEnd,
              toLines,
              toStart,
              toEnd,
              fromIndex,
              toIndex,
            )
          }
        }
      }
    }

    for (
      let diagonal = -distance + reverseStart;
      diagonal <= distance - reverseEnd;
      diagonal += 2
    ) {
      const vectorIndex = offset + diagonal
      let fromIndex = diagonal === -distance
        || (diagonal !== distance && reverse[vectorIndex - 1] < reverse[vectorIndex + 1])
        ? reverse[vectorIndex + 1]
        : reverse[vectorIndex - 1] + 1
      let toIndex = fromIndex - diagonal
      while (
        fromIndex < fromLength
        && toIndex < toLength
        && fromLines[fromEnd - fromIndex - 1] === toLines[toEnd - toIndex - 1]
      ) {
        fromIndex += 1
        toIndex += 1
      }
      reverse[vectorIndex] = fromIndex

      if (fromIndex > fromLength) reverseEnd += 2
      else if (toIndex > toLength) reverseStart += 2
      else if (!frontOverlap) {
        const forwardIndex = offset + delta - diagonal
        if (forwardIndex >= 0 && forwardIndex < vectorLength && forward[forwardIndex] !== -1) {
          const splitFrom = forward[forwardIndex]
          const splitTo = splitFrom - (forwardIndex - offset)
          if (splitFrom >= fromLength - fromIndex) {
            return splitAt(
              fromLines,
              fromStart,
              fromEnd,
              toLines,
              toStart,
              toEnd,
              splitFrom,
              splitTo,
            )
          }
        }
      }
    }
  }

  return replaceAll(fromLines, fromStart, fromEnd, toLines, toStart, toEnd)
}

function splitAt(
  fromLines: readonly string[],
  fromStart: number,
  fromEnd: number,
  toLines: readonly string[],
  toStart: number,
  toEnd: number,
  splitFrom: number,
  splitTo: number,
): LineEdit[] {
  const fromLength = fromEnd - fromStart
  const toLength = toEnd - toStart
  if (
    (splitFrom === 0 && splitTo === 0)
    || (splitFrom === fromLength && splitTo === toLength)
  ) {
    return replaceAll(fromLines, fromStart, fromEnd, toLines, toStart, toEnd)
  }
  return [
    ...diffRange(
      fromLines,
      fromStart,
      fromStart + splitFrom,
      toLines,
      toStart,
      toStart + splitTo,
    ),
    ...diffRange(
      fromLines,
      fromStart + splitFrom,
      fromEnd,
      toLines,
      toStart + splitTo,
      toEnd,
    ),
  ]
}

function replaceAll(
  fromLines: readonly string[],
  fromStart: number,
  fromEnd: number,
  toLines: readonly string[],
  toStart: number,
  toEnd: number,
): LineEdit[] {
  const edits: LineEdit[] = []
  for (let index = fromStart; index < fromEnd; index += 1) {
    edits.push({ kind: 'removed', line: fromLines[index] })
  }
  for (let index = toStart; index < toEnd; index += 1) {
    edits.push({ kind: 'added', line: toLines[index] })
  }
  return edits
}

function compact(edits: readonly LineEdit[]): readonly LineDiffSegment[] {
  const segments: { kind: LineDiffKind; lines: string[] }[] = []
  for (const edit of edits) {
    const previous = segments.at(-1)
    if (previous?.kind === edit.kind) previous.lines.push(edit.line)
    else segments.push({ kind: edit.kind, lines: [edit.line] })
  }
  for (let index = 0; index < segments.length - 1; index += 1) {
    if (segments[index].kind === 'added' && segments[index + 1].kind === 'removed') {
      const added = segments[index]
      segments[index] = segments[index + 1]
      segments[index + 1] = added
      index += 1
    }
  }
  return segments
}
